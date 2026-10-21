#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
程序化生成 8-bit / synth 风格音效与 BGM（numpy，无外部素材依赖）。
输出到 Assets/_Project/Resources/Audio/*.wav （22050Hz? 用 44100 单声道 16bit）
幂等：重复执行覆盖同名文件。
"""
import os
import struct
import wave

import numpy as np

SR = 44100
OUT = os.path.join(r"D:\unity\dash", "Assets", "_Project", "Resources", "Audio")


def save_wav(name, data, sr=SR):
    os.makedirs(OUT, exist_ok=True)
    data = np.clip(data, -1.0, 1.0)
    pcm = (data * 32767).astype("<i2")
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(pcm.tobytes())
    print("wrote", path, "%.2fs" % (len(data) / sr))


def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def env_ad(dur, attack=0.005, decay=None, curve=3.0):
    """attack-decay 包络"""
    n = int(SR * dur)
    t = np.arange(n) / SR
    a = max(1, int(SR * attack))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a)
    d = n - a
    if d > 0:
        e[a:] = np.exp(-curve * np.linspace(0, 1, d))
    return e


def onepole_lp(x, cutoff_curve):
    """时变单极点低通，cutoff_curve 是每个采样点的截止频率"""
    y = np.zeros_like(x)
    acc = 0.0
    for i in range(len(x)):
        fc = cutoff_curve[i]
        a = 1.0 - np.exp(-2.0 * np.pi * fc / SR)
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def sweep_cutoff(dur, f0, f1, power=1.0):
    n = int(SR * dur)
    return f0 + (f1 - f0) * (np.linspace(0, 1, n) ** power)


def noise(dur, seed=1):
    rng = np.random.default_rng(seed)
    return rng.uniform(-1, 1, int(SR * dur))


def sine_sweep(dur, f0, f1, power=1.0):
    n = int(SR * dur)
    f = f0 + (f1 - f0) * (np.linspace(0, 1, n) ** power)
    phase = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(phase)


def square_sweep(dur, f0, f1, duty=0.5):
    n = int(SR * dur)
    f = f0 + (f1 - f0) * np.linspace(0, 1, n)
    phase = np.cumsum(f) / SR
    return np.where((phase % 1.0) < duty, 1.0, -1.0)


def saw_sweep(dur, f0, f1):
    n = int(SR * dur)
    f = f0 + (f1 - f0) * np.linspace(0, 1, n)
    phase = np.cumsum(f) / SR
    return 2.0 * (phase % 1.0) - 1.0


# ---------------- 音效 ----------------

def sfx_dash():
    """冲刺：气流冲开 + 低频推背"""
    dur = 0.5
    nz = noise(dur, seed=7)
    body = onepole_lp(nz, sweep_cutoff(dur, 400, 5200, 0.6)) * env_ad(dur, 0.004, curve=3.2)
    sub = sine_sweep(dur, 150, 55, 0.5) * env_ad(dur, 0.01, curve=4.0) * 0.8
    air = sine_sweep(dur, 220, 900, 1.6) * env_ad(dur, 0.02, curve=5.0) * 0.18
    return np.tanh((body * 0.85 + sub + air) * 1.6)


def sfx_impact():
    """冲刺落地/冲击波：闷响 + 碎噪"""
    dur = 0.35
    thud = sine_sweep(dur, 120, 38, 0.35) * env_ad(dur, 0.002, curve=5.0)
    crack = onepole_lp(noise(dur, 3), sweep_cutoff(dur, 6000, 300, 1.4)) * env_ad(dur, 0.001, curve=6.0) * 0.6
    return np.tanh((thud * 1.2 + crack) * 1.5)


def sfx_hit():
    """命中：短促金属"""
    dur = 0.14
    tone = square_sweep(dur, 520, 180, 0.35) * env_ad(dur, 0.001, curve=6.0)
    nz = onepole_lp(noise(dur, 11), sweep_cutoff(dur, 8000, 2000, 1.0)) * env_ad(dur, 0.001, curve=9.0) * 0.5
    return np.tanh((tone * 0.7 + nz) * 1.4)


def sfx_kill():
    """击杀：下坠 + 噪声尾巴"""
    dur = 0.4
    body = square_sweep(dur, 420, 70, 0.25) * env_ad(dur, 0.002, curve=3.0) * 0.7
    nz = onepole_lp(noise(dur, 19), sweep_cutoff(dur, 4000, 500, 1.2)) * env_ad(dur, 0.005, curve=2.5) * 0.5
    return np.tanh((body + nz) * 1.3)


def sfx_jump():
    dur = 0.12
    return square_sweep(dur, 300, 760, 0.4) * env_ad(dur, 0.002, curve=7.0) * 0.55


def sfx_land():
    dur = 0.12
    thud = sine_sweep(dur, 160, 60, 0.5) * env_ad(dur, 0.001, curve=8.0)
    nz = onepole_lp(noise(dur, 5), sweep_cutoff(dur, 2500, 400, 1.0)) * env_ad(dur, 0.001, curve=9.0) * 0.35
    return np.tanh((thud + nz) * 1.2)


def sfx_hurt():
    dur = 0.3
    body = saw_sweep(dur, 320, 90) * env_ad(dur, 0.003, curve=4.0) * 0.55
    nz = onepole_lp(noise(dur, 23), sweep_cutoff(dur, 5000, 800, 1.0)) * env_ad(dur, 0.002, curve=5.0) * 0.45
    return np.tanh((body + nz) * 1.3)


def sfx_select():
    a = square_sweep(0.06, 880, 880) * env_ad(0.06, 0.001, curve=8.0)
    b = square_sweep(0.09, 1320, 1320) * env_ad(0.09, 0.001, curve=7.0)
    return np.concatenate([a, b]) * 0.35


def sfx_portal():
    """通关：上扬琶音"""
    notes = [523.25, 659.25, 783.99, 1046.5]
    parts = []
    for i, f in enumerate(notes):
        d = 0.16
        seg = square_sweep(d, f, f * 1.005, 0.4) * env_ad(d, 0.003, curve=3.0)
        parts.append(seg)
    out = np.concatenate(parts)
    # 加一点尾音
    tail = square_sweep(0.5, 1046.5, 1046.5, 0.4) * env_ad(0.5, 0.005, curve=3.5) * 0.5
    return np.concatenate([out, tail]) * 0.32


# ---------------- BGM ----------------

def bgm_loop(bars=8, bpm=104):
    """小调暗色电子循环：bass + arp + pad + 轻鼓"""
    beat = 60.0 / bpm
    bar = beat * 4
    total = bar * bars
    n = int(SR * total)
    out = np.zeros(n)

    def add(sig, start):
        i = int(SR * start)
        end = min(n, i + len(sig))
        if end > i:
            out[i:end] += sig[: end - i]

    # A 小调音阶
    root = 55.0  # A1
    prog = [0, 0, 3, 3, 5, 5, 7, 3]  # 半音偏移（A A C C D D E C）

    for b in range(bars):
        base = root * 2 ** (prog[b] / 12.0)
        # bass：8 分音符脉冲
        for k in range(8):
            d = beat * 0.45
            f = base * (2 if k % 4 == 3 else 1)
            seg = saw_sweep(d, f, f) * env_ad(d, 0.005, curve=4.5) * 0.30
            seg = onepole_lp(seg, np.full(len(seg), 320.0))
            add(seg, b * bar + k * beat * 0.5)

        # arp：16 分音符
        scale = [0, 3, 5, 7, 10, 12, 15]
        for k in range(16):
            step = k % 7
            octv = 2 if (k // 7) % 2 == 0 else 4
            f = base * octv * 2 ** (scale[step] / 12.0)
            d = beat * 0.16
            seg = square_sweep(d, f, f, 0.28) * env_ad(d, 0.002, curve=9.0) * 0.10
            add(seg, b * bar + k * beat * 0.25)

        # pad：每两拍一个和弦（根音 + 五度）
        for k in range(2):
            d = beat * 2
            f1 = base * 4
            seg = (saw_sweep(d, f1, f1) + saw_sweep(d, f1 * 1.5, f1 * 1.5)) * 0.05
            seg = onepole_lp(seg, np.full(len(seg), 900.0))
            seg *= env_ad(d, 0.15, curve=1.2)
            add(seg, b * bar + k * beat * 2)

        # 鼓
        for k in range(4):
            if k in (0, 2):
                d = 0.13
                kick = sine_sweep(d, 120, 45, 0.4) * env_ad(d, 0.001, curve=9.0) * 0.5
                add(kick, b * bar + k * beat)
            if k in (1, 3):
                d = 0.06
                hat = onepole_lp(noise(d, 31 + k + b), np.full(int(SR * d), 9000.0))
                hat = hat * env_ad(d, 0.001, curve=14.0) * 0.07
                add(hat, b * bar + k * beat)

    # 首尾交叉淡化 5ms 防爆音
    fade = int(SR * 0.005)
    out[:fade] *= np.linspace(0, 1, fade)
    out[-fade:] *= np.linspace(1, 0, fade)
    return np.tanh(out * 1.1) * 0.9


def main():
    save_wav("sfx_dash", sfx_dash())
    save_wav("sfx_impact", sfx_impact())
    save_wav("sfx_hit", sfx_hit())
    save_wav("sfx_kill", sfx_kill())
    save_wav("sfx_jump", sfx_jump())
    save_wav("sfx_land", sfx_land())
    save_wav("sfx_hurt", sfx_hurt())
    save_wav("sfx_select", sfx_select())
    save_wav("sfx_portal", sfx_portal())
    save_wav("bgm_loop", bgm_loop())
    print("done")


if __name__ == "__main__":
    main()
