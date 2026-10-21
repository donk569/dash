using UnityEngine;
using UnityEngine.SceneManagement;

namespace TiantaiDash
{
    /// <summary>关卡进度、统一重开、关卡切换、存档读写。</summary>
    public class LevelManager : MonoBehaviour
    {
        public const string UnlockedLevelKey = "UnlockedLevel";

        /// <summary>重开：直接重载场景，最干净，杜绝对象/事件残留。</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            var gm = GameManager.Instance;
            if (gm != null) gm.ResetRuntime();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void LoadLevel(int buildIndex)
        {
            Time.timeScale = 1f;
            if (buildIndex >= 0 && buildIndex < SceneManager.sceneCountInBuildSettings)
                SceneManager.LoadScene(buildIndex);
            else
                RestartLevel();
        }

        public void LoadNextLevel()
        {
            // 首版只有一关，下一关 = 重开当前关
            RestartLevel();
        }

        public int GetUnlockedLevel()
        {
            return PlayerPrefs.GetInt(UnlockedLevelKey, 1);
        }

        public void UnlockLevel(int level)
        {
            if (level <= GetUnlockedLevel()) return;
            PlayerPrefs.SetInt(UnlockedLevelKey, level);
            PlayerPrefs.Save();
        }
    }
}
