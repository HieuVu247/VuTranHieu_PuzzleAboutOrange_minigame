using UnityEngine;
using UnityEngine.UI; // Cần thiết cho các yếu tố UI cơ bản như Image
using TMPro; // Cần thiết cho TextMeshPro
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("HUD Elements")]
    [Tooltip("Text để hiển thị điểm số")]
    public TextMeshProUGUI scoreText;

    [Tooltip("Text để hiển thị thời gian còn lại")]
    public TextMeshProUGUI timeText;

    [Header("Game Over Panel")]
    [Tooltip("Panel chứa tất cả các yếu tố của màn hình Game Over")]
    public GameObject gameOverPanel;
    [Tooltip("Text để hiển thị điểm số cuối cùng")]
    public TextMeshProUGUI finalScoreText;
    [Tooltip("Đối tượng (Text hoặc Image) để hiển thị thông báo Kỷ lục mới")]
    public GameObject newHighScoreIndicator;

    [Header("Game Logic Reference")]
    [Tooltip("Tham chiếu đến BoardManager để gọi hàm StartNewGame")]
    public BoardManager boardManager; // Thêm tham chiếu đến BoardManager
    
    void Start()
    {
        // Đảm bảo các panel bị ẩn khi bắt đầu
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (newHighScoreIndicator != null) newHighScoreIndicator.SetActive(false);
    }
    
    // Hàm này sẽ được BoardManager gọi để cập nhật hiển thị điểm
    public void UpdateScoreDisplay(int newScore)
    {
        if (scoreText != null)
        {
            scoreText.text = newScore.ToString();
        }
    }

    // Hàm này sẽ được BoardManager gọi để cập nhật hiển thị thời gian
    public void UpdateTimeDisplay(float time)
    {
        if (timeText != null)
        {
            if (time < 0) time = 0;

            // Làm tròn lên số giây gần nhất để hiển thị cho người chơi (ví dụ: 44.1s vẫn hiện là 45)
            timeText.text = Mathf.CeilToInt(time).ToString();
        }
    }
    
    public void ShowGameOverScreen(int finalScore, bool isNewHighScore)
    {
        if (gameOverPanel == null) return;

        gameOverPanel.SetActive(true);
        finalScoreText.text = finalScore.ToString();

        if (newHighScoreIndicator != null)
        {
            newHighScoreIndicator.SetActive(isNewHighScore);
        }
    }
    public void ReplayButtonHandler()
    {
        if (boardManager != null)
        {
            // Ẩn màn hình game over trước khi bắt đầu game mới
            gameOverPanel.SetActive(false);
            newHighScoreIndicator.SetActive(false);
            // Gọi hàm StartNewGame trên BoardManager
            boardManager.StartNewGame();
        }
        else
        {
            Debug.LogError("Chưa tham chiếu BoardManager trên UIManager!");
        }
    }

    // HÀM MỚI: Được gọi bởi nút "Về Menu"
    public void GoToMainMenuButtonHandler()
    {
        // Reset Time.timeScale về 1 trước khi chuyển scene, phòng trường hợp game đang bị dừng
        Time.timeScale = 1; 
        SceneManager.LoadScene("MainMenu");
    }
}