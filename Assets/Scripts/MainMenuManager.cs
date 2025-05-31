using UnityEngine;
using UnityEngine.SceneManagement; // Cần thiết để quản lý và chuyển đổi scene
using TMPro; // Cần thiết để làm việc với TextMeshPro

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text để hiển thị điểm cao nhất")]
    [SerializeField] private TextMeshProUGUI highScoreText;

    // Hàm Awake được gọi một lần khi script được tải
    void Awake()
    {
        // Đặt Time.timeScale = 1 phòng trường hợp người chơi thoát ra từ màn chơi đã bị tạm dừng
        Time.timeScale = 1; 
        
        DisplayHighScore();
    }

    // Hàm đọc và hiển thị điểm cao nhất đã được lưu
    void DisplayHighScore()
    {
        // PlayerPrefs là một cách đơn giản để lưu dữ liệu nhỏ trên máy của người chơi.
        // PlayerPrefs.GetInt("HighScore", 0) sẽ:
        // - Cố gắng đọc giá trị có key là "HighScore".
        // - Nếu không tìm thấy key đó (lần đầu chơi), nó sẽ trả về giá trị mặc định là 0.
        int savedHighScore = PlayerPrefs.GetInt("HighScore", 0);

        if (highScoreText != null)
        {
            highScoreText.text = "High Score: " + savedHighScore.ToString();
        }
    }

    // Hàm này sẽ được gọi khi người chơi nhấn nút "Play"
    public void PlayGame()
    {
        Debug.Log("Chuyển sang scene Gameplay...");
        // Tải scene có tên là "Gameplay". Tên này phải khớp chính xác với tên file scene của bạn.
        SceneManager.LoadScene("Gameplay");
    }

    // (Tùy chọn) Có thể thêm hàm thoát game nếu bạn build cho PC
    public void QuitGame()
    {
        Debug.Log("Thoát game!");
        Application.Quit();
    }
}