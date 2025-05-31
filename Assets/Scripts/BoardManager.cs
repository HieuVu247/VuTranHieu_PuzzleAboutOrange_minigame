using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ĐỊNH NGHĨA CẤU TRÚC DỮ LIỆU CHO LEVEL
[System.Serializable] // Giúp Unity có thể hiển thị struct này trong Inspector nếu cần
public struct LevelConfig
{
    public int levelDisplayID;    // Level để hiển thị cho người chơi (1, 2, 3)
    public int gridWidth;
    public int gridHeight;
    public int blockCount;
    public int shuffleDepth;      // Số "nước đi người chơi" dùng để xáo trộn

    // Constructor để dễ tạo đối tượng LevelConfig
    public LevelConfig(int id, int width, int height, int blocks, int depth)
    {
        levelDisplayID = id;
        gridWidth = width;
        gridHeight = height;
        blockCount = blocks;
        shuffleDepth = depth;
    }
}
// Đây là "bộ não" chính của game, quản lý mọi thứ trên bàn cờ.
public class BoardManager : MonoBehaviour
{
    // =================================================================
    // CÁC BIẾN TÙY CHỈNH (CONFIGURABLE PARAMETERS)
    // Bạn có thể thay đổi các giá trị này trực tiếp trên Unity Editor.
    // [SerializeField] giúp các biến private này hiện ra trên Inspector.
    // =================================================================
    
    [Header("Prefabs")]
    [Tooltip("Prefab cho khối Chặn")]
    [SerializeField] private GameObject blockPrefab;
    
    [Tooltip("Mảng chứa 4 Prefab của 4 mảnh chanh. Sắp xếp theo thứ tự: Trái-Trên, Phải-Trên, Trái-Dưới, Phải-Dưới")]
    [SerializeField] private GameObject[] lemonPiecePrefabs = new GameObject[4];
    
    [Header("Input Settings")]
    [Tooltip("Khoảng cách vuốt tối thiểu (bằng pixel) để được tính là một lần di chuyển")]
    [SerializeField] private float minSwipeDistance = 50f;
    
    [Header("UI Manager Reference")]
    public UIManager uiManager;
    
    [Header("Environment References")]
    [Tooltip("Sprite Renderer cho hình nền phía sau lưới.")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [Tooltip("Sprite Renderer cho khung (đã thiết lập 9-slice).")]
    [SerializeField] private SpriteRenderer frameRenderer;
    // =================================================================
    // BIẾN NỘI BỘ (INTERNAL STATE)
    // =================================================================
    
    // Một mảng 2 chiều để lưu trữ logic của lưới.
    // Chúng ta sẽ lưu Transform của object tại mỗi tọa độ (x, y).
    // Nếu một ô không có gì, giá trị sẽ là null.
    private Transform[,] grid;
    
    // Một danh sách để tiện truy cập tới các mảnh chanh đã được tạo ra.
    private List<GameObject> lemonPieceInstances = new List<GameObject>();

    // === BIẾN CHO QUẢN LÝ LEVEL VÀ TRẠNG THÁI GAME ===
    private List<LevelConfig> allLevelConfigs;
    private LevelConfig currentActiveLevelConfig; // Config của level đang được sử dụng để tạo puzzle

    private int currentScore;
    private int currentDisplayLevelNumber; // Level hiển thị cho người dùng (1, 2, hoặc 3)
    private float timeRemainingThisAttempt;
    private const float TIME_PER_ATTEMPT = 45.0f; // Thời gian cho mỗi câu đố
    private bool isGameOverActive;

    // Dùng để lưu trạng thái ban đầu của các mảnh chanh sau khi xáo trộn, cho chức năng Reset Puzzle
    private List<Vector3> initialLemonPiecePositionsForReset;
    private List<GameObject> lemonPieceOrderForReset;
    
    // Vị trí khi người chơi bắt đầu nhấn chuột
    private Vector2 startMousePosition; 
    void Awake()
    {
        InitializeAllLevelConfigs(); // Khởi tạo danh sách các cấu hình level

        // Khởi tạo các List để tránh lỗi NullReference
        initialLemonPiecePositionsForReset = new List<Vector3>();
        lemonPieceOrderForReset = new List<GameObject>();
        // lemonPieceInstances đã được khởi tạo rồi: private List<GameObject> lemonPieceInstances = new List<GameObject>();
    }

    void InitializeAllLevelConfigs()
    {
        allLevelConfigs = new List<LevelConfig>
        {
            // Level 1: 4x4, 3 khối chặn, 5 lần đảo
            new LevelConfig(1, 4, 4, 3, 20),
            // Level 2: 5x5, 5 khối chặn, 10 lần đảo
            new LevelConfig(2, 5, 5, 5, 25),
            // Level 3: 5x5, 8 khối chặn, 15 lần đảo
            new LevelConfig(3, 5, 5, 8, 30)
        };
    }
    void Start()
    {
        StartNewGame();
    }
    public void StartNewGame() // Để public phòng trường hợp nút UI gọi
    {
        currentScore = 0;
        currentDisplayLevelNumber = 1; // Luôn bắt đầu từ Level 1
        isGameOverActive = false;
        Time.timeScale = 1; // Đảm bảo game chạy nếu trước đó đã bị dừng

        Debug.Log("Bắt đầu game mới! Level 1, Điểm: 0.");
        if (uiManager != null) uiManager.UpdateScoreDisplay(currentScore);
        LoadPuzzleForCurrentLevelConfig();
    }
    void LoadPuzzleForCurrentLevelConfig()
    {
        if (isGameOverActive) return; // Không làm gì nếu game đã over

        // Tìm config phù hợp cho currentDisplayLevelNumber
        // Vì allLevelConfigs[0] là Level 1, allLevelConfigs[1] là Level 2,...
        // nên index sẽ là currentDisplayLevelNumber - 1
        int configIndex = currentDisplayLevelNumber - 1;

        if (configIndex < 0 || configIndex >= allLevelConfigs.Count)
        {
            Debug.LogError($"Không tìm thấy cấu hình cho Level {currentDisplayLevelNumber}. Kiểm tra lại allLevelConfigs.");
            isGameOverActive = true; // Lỗi nghiêm trọng, dừng game
            return;
        }
        currentActiveLevelConfig = allLevelConfigs[configIndex];
        //HÀM CẬP NHẬT CAMERA VÀ MÔI TRƯỜNG
        UpdateCameraAndEnvironment(currentActiveLevelConfig);
        // Reset thời gian
        timeRemainingThisAttempt = TIME_PER_ATTEMPT; 
        
        Debug.Log($"Đang tải câu đố cho Level {currentActiveLevelConfig.levelDisplayID} (Điểm: {currentScore}). " +
                  $"Lưới: {currentActiveLevelConfig.gridWidth}x{currentActiveLevelConfig.gridHeight}, " +
                  $"Khối: {currentActiveLevelConfig.blockCount}, Đảo: {currentActiveLevelConfig.shuffleDepth}. " +
                  $"Thời gian: {timeRemainingThisAttempt}s");
        //UI cập nhật thời gian
        if (uiManager != null) uiManager.UpdateTimeDisplay(timeRemainingThisAttempt);

        // Tạo grid logic mới với kích thước từ config
        // Hàm ClearBoard sẽ xóa các GameObject cũ và reset lemonPieceInstances
        ClearBoard(); // Cần đảm bảo hàm này được triển khai đúng
        grid = new Transform[currentActiveLevelConfig.gridWidth, currentActiveLevelConfig.gridHeight];

        // Bắt đầu chuỗi tạo màn chơi với config đã chọn
        StartCoroutine(GenerateLevelSequence(currentActiveLevelConfig));
    }
    void ClearBoard()
    {
        // Hủy các GameObject mảnh chanh cũ
        foreach (var pieceInstance in lemonPieceInstances)
        {
            if (pieceInstance != null) Destroy(pieceInstance);
        }
        lemonPieceInstances.Clear();

        // Hủy các GameObject khối chặn cũ và dọn dẹp grid logic
        if (grid != null)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y] != null)
                    {
                        // Nếu là khối chặn thì hủy, mảnh chanh đã hủy ở trên rồi
                        if (!lemonPieceInstances.Contains(grid[x, y].gameObject)) // Kiểm tra đơn giản
                        {
                            // Để chắc chắn hơn, bạn có thể dùng tag hoặc component để nhận diện block
                            if (grid[x, y].CompareTag("Block")) // Nếu bạn đặt tag "Block" cho prefab khối chặn
                            {
                                Destroy(grid[x, y].gameObject);
                            }
                        }
                        grid[x, y] = null;
                    }
                }
            }
        }
        // Reset grid về null để LoadLevel có thể tạo lại với kích thước mới
        // grid = null; // Không cần thiết nếu bạn tạo grid mới ngay trong LoadLevel như trên
    }
    void Update()
    {
        if (isGameOverActive) // Nếu game đã over, không làm gì cả
        {
            return;
        }

        // XỬ LÝ INPUT NGƯỜI CHƠI (đã có)
        if (Input.GetMouseButtonDown(0))
        {
            startMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            Vector2 endMousePosition = Input.mousePosition;
            Vector2 swipeVector = endMousePosition - startMousePosition;

            if (swipeVector.magnitude > minSwipeDistance)
            {
                ProcessSwipe(swipeVector);
            }
        }

        // XỬ LÝ ĐẾM NGƯỢC THỜI GIAN
        if (timeRemainingThisAttempt > 0)
        {
            timeRemainingThisAttempt -= Time.deltaTime;
            //UI
            if (uiManager != null) uiManager.UpdateTimeDisplay(timeRemainingThisAttempt);

            if (timeRemainingThisAttempt <= 0)
            {
                timeRemainingThisAttempt = 0; // Đảm bảo không hiển thị số âm
                if (uiManager != null) uiManager.UpdateTimeDisplay(timeRemainingThisAttempt);
                HandleTimeUpGameOver();
            }
        }
    }
    void HandleTimeUpGameOver()
    {
        isGameOverActive = true;
        Time.timeScale = 0; // Dừng game lại để người chơi không thể tương tác nữa

        // XỬ LÝ HIGH SCORE
        int savedHighScore = PlayerPrefs.GetInt("HighScore", 0);
        bool isNewHighScore = false;

        if (currentScore > savedHighScore)
        {
            PlayerPrefs.SetInt("HighScore", currentScore);
            PlayerPrefs.Save(); // Đảm bảo dữ liệu được ghi xuống đĩa
            isNewHighScore = true;
            Debug.Log($"KỶ LỤC MỚI! Điểm mới: {currentScore}. Kỷ lục cũ: {savedHighScore}");
        }
        
        Debug.Log($"HẾT GIỜ! GAME OVER. Điểm cuối cùng: {currentScore}");

        // GỌI UI MANAGER ĐỂ HIỂN THỊ MÀN HÌNH GAME OVER
        if (uiManager != null)
        {
            uiManager.ShowGameOverScreen(currentScore, isNewHighScore);
        }
    }
    // Hàm xử lý logic sau khi một cú vuốt hợp lệ được thực hiện
    private void ProcessSwipe(Vector2 swipeVector)
    {
        if (isGameOverActive) return;

        Vector2Int moveDirection = Vector2Int.zero;
        if (Mathf.Abs(swipeVector.x) > Mathf.Abs(swipeVector.y))
        {
            moveDirection = (swipeVector.x > 0) ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            moveDirection = (swipeVector.y > 0) ? Vector2Int.up : Vector2Int.down;
        }
        
        bool hasMoved = AttemptToMovePieces(moveDirection, false); 

        if (hasMoved)
        {
            Debug.Log("Người chơi đã di chuyển.");
            CheckWinConditionAfterPlayerMove(); // Gọi hàm kiểm tra thắng/lên level
        }
    }
    void CheckWinConditionAfterPlayerMove()
    {
        if (isGameOverActive) return; // Không kiểm tra nếu game đã over

        if (IsWinConditionMet())
        {
            currentScore++;
            Debug.Log($"GIẢI ĐƯỢC CÂU ĐỐ! Điểm hiện tại: {currentScore}");
            //UI
            if (uiManager != null) uiManager.UpdateScoreDisplay(currentScore);

            // Kiểm tra điều kiện lên level
            bool leveledUp = false;
            if (currentDisplayLevelNumber == 1 && currentScore >= 5)
            {
                currentDisplayLevelNumber = 2;
                leveledUp = true;
                Debug.Log("CHÚC MỪNG! Lên Level 2!");
            }
            else if (currentDisplayLevelNumber == 2 && currentScore >= 15)
            {
                currentDisplayLevelNumber = 3;
                leveledUp = true;
                Debug.Log("CHÚC MỪNG! Lên Level 3!");
            }
            // (Thêm điều kiện nếu có level cao hơn nữa)

            // Nếu đã ở level cuối cùng (Level 3) và tiếp tục ghi điểm, có thể không cần thông báo gì đặc biệt
            // Hoặc có thể có một thông báo hoàn thành DEMO nếu bạn muốn kết thúc game sau Level 3.
            if (currentDisplayLevelNumber == 3 && leveledUp) // Vừa lên level 3
            {
                // Có thể có thông báo đặc biệt cho level cuối
            }
             // Nếu đã hoàn thành level 3 (ví dụ, giải thêm 1 puzzle sau khi lên lv3)
            else if (currentDisplayLevelNumber == 3 && currentScore > 15 && allLevelConfigs.Count == 3) 
            {
                // Giả sử 15 là điểm để đạt level 3. Sau đó giải thêm sẽ chỉ tăng điểm.
                // Nếu bạn muốn có một trạng thái "Hoàn thành Demo" đặc biệt sau khi giải 1 câu đố ở Level 3:
                // isGameOverActive = true; // Đánh dấu là game đã "hoàn thành"
                // Time.timeScale = 0;
                // Debug.Log("XUẤT SẮC! Bạn đã hoàn thành toàn bộ DEMO!");
                // (UI) Hiển thị màn hình hoàn thành Demo.
                // Hiện tại, chúng ta sẽ cho phép chơi tiếp ở Level 3 để tích điểm.
            }


            // Tải câu đố tiếp theo (cho level hiện tại hoặc level mới)
            LoadPuzzleForCurrentLevelConfig();
        }
    }
    private bool IsWinConditionMet()
    {
        if (lemonPieceInstances.Count < 4) return false;

        // 1. Tìm một mảnh làm mốc (ví dụ: mảnh có prefab giống lemonPiecePrefabs[0] - mảnh Trái-Trên)
        GameObject referencePieceGO = null; // Mảnh chanh được dùng làm tham chiếu (ví dụ: mảnh trên-trái)
        Vector2Int referencePieceGridPos = Vector2Int.zero; // Vị trí lưới của mảnh tham chiếu

        foreach (var pieceInstance in lemonPieceInstances)
        {
            // So sánh tên của instance với tên prefab gốc. 
            // Ví dụ: nếu prefab là "Lemon_TL", instance sẽ là "Lemon_TL(Clone)"
            if (pieceInstance.name.StartsWith(lemonPiecePrefabs[0].name))
            {
                referencePieceGO = pieceInstance;
                referencePieceGridPos = new Vector2Int(
                    Mathf.RoundToInt(referencePieceGO.transform.position.x),
                    Mathf.RoundToInt(referencePieceGO.transform.position.y)
                );
                break;
            }
        }

        if (referencePieceGO == null)
        {
            // Debug.LogWarning("Không tìm thấy mảnh chanh tham chiếu (Trái-Trên) để kiểm tra thắng.");
            return false; // Không có mảnh tham chiếu thì không kiểm tra được
        }

        // 2. Xác định vị trí tương đối mong đợi của các mảnh khác so với mảnh tham chiếu
        // Giả sử lemonPiecePrefabs được sắp xếp: [0]=TL, [1]=TR, [2]=BL, [3]=BR
        // Và hệ tọa độ Y tăng khi đi LÊN (như trong PlaceSolvedLemon)
        // Offset từ vị trí của mảnh Trái-Trên (TL) đến các mảnh khác:
        // TL -> TL: (0,0)
        // TL -> TR: (+1,0)
        // TL -> BL: (0,-1)
        // TL -> BR: (+1,-1)
        Vector2Int[] expectedRelativeOffsets = {
            new Vector2Int(0, 0),  // Vị trí của lemonPiecePrefabs[0] (TL) so với chính nó
            new Vector2Int(1, 0),  // Vị trí của lemonPiecePrefabs[1] (TR) so với TL
            new Vector2Int(0, -1), // Vị trí của lemonPiecePrefabs[2] (BL) so với TL
            new Vector2Int(1, -1)  // Vị trí của lemonPiecePrefabs[3] (BR) so với TL
        };
        // Nếu hệ tọa độ Y của bạn tăng khi đi XUỐNG, bạn cần đảo ngược dấu Y trong offsets,
        // ví dụ BL sẽ là (0,1), BR sẽ là (1,1) so với TL.

        // 3. Kiểm tra từng loại mảnh prefab xem có nằm đúng vị trí tương đối đã định không
        for (int i = 0; i < lemonPiecePrefabs.Length; i++)
        {
            GameObject expectedPrefabType = lemonPiecePrefabs[i];
            Vector2Int expectedFinalPos = referencePieceGridPos + expectedRelativeOffsets[i];
            bool foundCorrectPieceAtExpectedPosition = false;

            foreach (var pieceInstance in lemonPieceInstances)
            {
                // Kiểm tra xem mảnh instance này có phải là loại prefab đang xét không
                if (pieceInstance.name.StartsWith(expectedPrefabType.name))
                {
                    Vector2Int actualPos = new Vector2Int(
                        Mathf.RoundToInt(pieceInstance.transform.position.x),
                        Mathf.RoundToInt(pieceInstance.transform.position.y)
                    );
                    if (actualPos == expectedFinalPos)
                    {
                        foundCorrectPieceAtExpectedPosition = true;
                        break; 
                    }
                }
            }

            if (!foundCorrectPieceAtExpectedPosition)
            {
                return false; // Nếu một loại mảnh không ở đúng vị trí -> chưa thắng
            }
        }

        return true; // Tất cả các loại mảnh đều ở đúng vị trí tương đối -> thắng
    }
    
    public void ResetCurrentPuzzleButtonHandler() // Để public cho nút UI gọi
    {
        if (isGameOverActive) return; // Không reset nếu game đã over
        if (initialLemonPiecePositionsForReset == null || initialLemonPiecePositionsForReset.Count != lemonPieceInstances.Count ||
            lemonPieceOrderForReset == null || lemonPieceOrderForReset.Count != lemonPieceInstances.Count)
        {
            Debug.LogWarning("Không có dữ liệu để reset puzzle hoặc dữ liệu không khớp.");
            return;
        }

        Debug.Log("Resetting puzzle to its initial shuffled state...");

        // Dọn dẹp trạng thái hiện tại của các mảnh chanh trên lưới logic
        foreach (var pieceInstance in lemonPieceInstances)
        {
            if (pieceInstance != null) // Kiểm tra null phòng trường hợp
            {
                Vector2Int currentPieceGridPos = new Vector2Int(
                    Mathf.RoundToInt(pieceInstance.transform.position.x),
                    Mathf.RoundToInt(pieceInstance.transform.position.y)
                );
                // Đảm bảo vị trí nằm trong lưới trước khi truy cập grid[,]
                if (currentPieceGridPos.x >= 0 && currentPieceGridPos.x < currentActiveLevelConfig.gridWidth &&
                    currentPieceGridPos.y >= 0 && currentPieceGridPos.y < currentActiveLevelConfig.gridHeight)
                {
                     if(grid[currentPieceGridPos.x, currentPieceGridPos.y] == pieceInstance.transform)
                     {
                        grid[currentPieceGridPos.x, currentPieceGridPos.y] = null;
                     }
                }
            }
        }

        // Đặt lại vị trí và cập nhật lưới logic cho từng mảnh chanh
        for (int i = 0; i < lemonPieceOrderForReset.Count; i++)
        {
            GameObject pieceToReset = lemonPieceOrderForReset[i];
            Vector3 savedPosition = initialLemonPiecePositionsForReset[i];

            if (pieceToReset != null)
            {
                pieceToReset.transform.position = savedPosition;
                Vector2Int newGridPos = new Vector2Int(Mathf.RoundToInt(savedPosition.x), Mathf.RoundToInt(savedPosition.y));
                
                // Đảm bảo vị trí nằm trong lưới trước khi gán
                if (newGridPos.x >= 0 && newGridPos.x < currentActiveLevelConfig.gridWidth &&
                    newGridPos.y >= 0 && newGridPos.y < currentActiveLevelConfig.gridHeight)
                {
                    grid[newGridPos.x, newGridPos.y] = pieceToReset.transform;
                }
                else
                {
                    Debug.LogError($"Vị trí đã lưu ({newGridPos}) cho mảnh {pieceToReset.name} nằm ngoài lưới khi reset!");
                }
            }
        }
        // Thời gian không reset, tiếp tục chạy
        // (UI) Có thể cần cập nhật lại hiển thị bàn cờ nếu có thay đổi đặc biệt
    }
    private IEnumerator GenerateLevelSequence(LevelConfig config) // Nhận LevelConfig
    {
        // ClearBoard() đã được gọi trong LoadPuzzleForCurrentLevelConfig TRƯỚC KHI gọi coroutine này
        // grid cũng đã được khởi tạo với kích thước mới trong LoadPuzzleForCurrentLevelConfig

        Debug.Log($"Đang tạo màn chơi cho Level {config.levelDisplayID}. " +
                  $"Lưới: {config.gridWidth}x{config.gridHeight}, " +
                  $"Khối: {config.blockCount}, Độ sâu xáo trộn: {config.shuffleDepth}");

        // 1. TẠO TRẠNG THÁI THẮNG
        Vector2Int solvedPositionAnchor = FindRandom2x2Area(config.gridWidth, config.gridHeight);
        if (solvedPositionAnchor.x == -1) // Kiểm tra nếu không tìm thấy vị trí
        {
            Debug.LogError("Không tìm thấy không gian 2x2 trống để đặt các mảnh chanh!");
            isGameOverActive = true; // Lỗi nghiêm trọng, dừng game
            yield break; 
        }
        PlaceSolvedLemon(solvedPositionAnchor); // Hàm này không cần thay đổi nếu nó chỉ dùng anchor

        // 2. ĐẶT CÁC KHỐI CHẶN
        PlaceBlocks(config.blockCount, config.gridWidth, config.gridHeight);

        // 3. HIỂN THỊ TRẠNG THÁI THẮNG (Ngắn thôi)
        // yield return new WaitForSeconds(0.5f); // Có thể bỏ qua hoặc giảm thời gian này

        // 4. XÁO TRỘN BÀN CỜ THEO KIỂU RUBIK
        Debug.Log("Bắt đầu xáo trộn 'kiểu Rubik'...");
        yield return StartCoroutine(ShuffleBoardRubikStyle(config.shuffleDepth, config.gridWidth, config.gridHeight));
        
        // 5. LƯU TRẠNG THÁI BAN ĐẦU SAU KHI XÁO TRỘN (CHO NÚT RESET)
        initialLemonPiecePositionsForReset.Clear();
        lemonPieceOrderForReset.Clear();
        foreach (GameObject pieceInstance in lemonPieceInstances)
        {
            initialLemonPiecePositionsForReset.Add(pieceInstance.transform.position);
            lemonPieceOrderForReset.Add(pieceInstance);
        }
        Debug.Log("Đã lưu trạng thái bàn cờ cho việc Reset.");

        Debug.Log($"Màn {config.levelDisplayID} sẵn sàng!");
    }
    // Tìm các ô trống ngẫu nhiên trong khung
    private Vector2Int FindRandom2x2Area(int currentGridWidth, int currentGridHeight) // Nhận kích thước lưới hiện tại
    {
        List<Vector2Int> validPositions = new List<Vector2Int>();
        for (int x = 0; x < currentGridWidth - 1; x++) // Dùng currentGridWidth
        {
            for (int y = 0; y < currentGridHeight - 1; y++) // Dùng currentGridHeight
            {
                if (grid[x, y] == null && 
                    grid[x + 1, y] == null && 
                    grid[x, y + 1] == null && // Chú ý: hệ tọa độ của bạn có thể khác
                    grid[x + 1, y + 1] == null)
                {
                    validPositions.Add(new Vector2Int(x, y));
                }
            }
        }

        if (validPositions.Count > 0)
        {
            return validPositions[Random.Range(0, validPositions.Count)];
        }
        return new Vector2Int(-1, -1); // Trả về giá trị không hợp lệ nếu không tìm thấy
    }
    // Đặt khối
    private void PlaceBlocks(int numberOfBlocks, int currentGridWidth, int currentGridHeight) // Nhận số khối và kích thước
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>();
        for (int x = 0; x < currentGridWidth; x++) // Dùng currentGridWidth
        {
            for (int y = 0; y < currentGridHeight; y++) // Dùng currentGridHeight
            {
                if (grid[x, y] == null)
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }

        for (int i = 0; i < numberOfBlocks; i++)
        {
            if (emptyCells.Count == 0)
            {
                Debug.LogWarning("Không đủ ô trống để đặt tất cả các khối chặn!");
                break;
            }
            int randomIndex = Random.Range(0, emptyCells.Count);
            Vector2Int pos = emptyCells[randomIndex];
            
            GameObject block = Instantiate(blockPrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            // Đặt tag cho block để ClearBoard dễ nhận diện hơn
            block.tag = "Block"; // Đảm bảo bạn đã tạo tag "Block" trong Unity Editor
            grid[pos.x, pos.y] = block.transform;
            emptyCells.RemoveAt(randomIndex);
        }
    }
    // Hàm đặt 4 mảnh chanh vào vị trí đã giải.
    private void PlaceSolvedLemon(Vector2Int anchor)
    {
        // anchor là tọa độ góc trên-bên trái của khối 2x2
        Vector2Int[] offsets = {
            new Vector2Int(0, 1), // Top-Left
            new Vector2Int(1, 1), // Top-Right
            new Vector2Int(0, 0), // Bottom-Left
            new Vector2Int(1, 0)  // Bottom-Right
        };
        
        for(int i = 0; i < 4; i++)
        {
            Vector2Int pos = anchor + offsets[i];
            // Instantiate là hàm của Unity để tạo một object từ Prefab.
            GameObject piece = Instantiate(lemonPiecePrefabs[i], new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            grid[pos.x, pos.y] = piece.transform;
            lemonPieceInstances.Add(piece);
        }
    }
    
    // Hàm thực hiện xáo trộn bàn cờ một cách có hình ảnh (visual)
    private IEnumerator ShuffleBoardRubikStyle(int shuffleDepth, int currentGridWidth, int currentGridHeight)
    {
        Debug.Log($"Xáo trộn 'kiểu Rubik' với {shuffleDepth} nước đi.");
        Vector2Int[] possibleDirections = {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        for (int i = 0; i < shuffleDepth; i++)
        {
            // Chọn một hướng ngẫu nhiên
            Vector2Int randomDirection = possibleDirections[Random.Range(0, possibleDirections.Length)];
            
            // Thực hiện nước đi "giống người chơi"
            // Hàm AttemptToMovePieces sẽ xử lý việc di chuyển các mảnh một cách độc lập
            // Chúng ta sẽ thêm một tham số bool để nó biết đây là nước đi shuffle (không tính điểm, không check win ngay)
            AttemptToMovePieces(randomDirection, true); // true nghĩa là isShufflingMove

            // Đợi một frame để các thay đổi được áp dụng hoàn toàn trước khi thực hiện nước shuffle tiếp theo.
            // Điều này cũng giúp tránh game bị treo nếu shuffleDepth lớn.
            // Bạn có thể thêm yield return new WaitForSeconds(0.01f); nếu muốn thấy quá trình shuffle chậm hơn.
            yield return null; 
        }
        Debug.Log("Hoàn thành xáo trộn 'kiểu Rubik'.");
    }
    // HÀM DI CHUYỂN MỚI - XỬ LÝ CÁC MẢNH GHÉP ĐỘC LẬP
    private bool AttemptToMovePieces(Vector2Int direction, bool isShufflingMove = false) // Thêm isShufflingMove
    {
        List<GameObject> sortedPieces = new List<GameObject>(lemonPieceInstances);
        bool anyPieceActuallyMoved = false; // Cờ để theo dõi xem có mảnh nào thực sự di chuyển không

        // SẮP XẾP (Logic cũ vẫn đúng)
        if (direction == Vector2Int.right)
            sortedPieces.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
        else if (direction == Vector2Int.left)
            sortedPieces.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        else if (direction == Vector2Int.up) // Giả sử Y tăng khi đi lên
            sortedPieces.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
        else if (direction == Vector2Int.down) // Giả sử Y tăng khi đi lên
            sortedPieces.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));

        foreach (var piece in sortedPieces)
        {
            Vector2Int currentPos = new Vector2Int(Mathf.RoundToInt(piece.transform.position.x), Mathf.RoundToInt(piece.transform.position.y));
            Vector2Int targetPos = currentPos + direction;

            // Kiểm tra biên dựa trên kích thước lưới hiện tại của level
            if (targetPos.x < 0 || targetPos.x >= currentActiveLevelConfig.gridWidth || 
                targetPos.y < 0 || targetPos.y >= currentActiveLevelConfig.gridHeight)
            {
                continue; 
            }

            if (grid[targetPos.x, targetPos.y] != null)
            {
                continue; 
            }

            grid[targetPos.x, targetPos.y] = piece.transform;
            grid[currentPos.x, currentPos.y] = null;
            piece.transform.position = new Vector3(targetPos.x, targetPos.y, 0);
            anyPieceActuallyMoved = true; // Đánh dấu có ít nhất một mảnh đã di chuyển
        }
        
        // Nếu là nước đi của người chơi (không phải shuffle) và có mảnh di chuyển, thì mới xử lý tiếp
        // Logic này đã được chuyển sang ProcessSwipe
        // if (!isShufflingMove && anyPieceActuallyMoved)
        // {
        //    // CheckWinConditionAfterPlayerMove(); // Sẽ được gọi từ ProcessSwipe
        // }
        return anyPieceActuallyMoved; // Trả về true nếu có ít nhất 1 mảnh di chuyển
    }
    
    private void UpdateCameraAndEnvironment(LevelConfig config)
    {
        // 1. Đặt vị trí Camera để tâm của lưới luôn ở giữa màn hình
        float gridCenterX = (config.gridWidth - 1) / 2.0f;
        float gridCenterY = (config.gridHeight - 1) / 2.0f;
        Camera.main.transform.position = new Vector3(gridCenterX, gridCenterY, -10f); // -10f là giá trị Z mặc định của camera

        // 2. Điều chỉnh kích thước Camera (Zoom) để vừa với lưới
        float screenRatio = (float)Screen.width / (float)Screen.height;
        float targetRatio = (float)config.gridWidth / (float)config.gridHeight;
        float padding = 1.0f; // Khoảng đệm nhỏ xung quanh lưới

        if (screenRatio >= targetRatio)
        {
            // Màn hình rộng hơn hoặc bằng lưới -> chiều cao quyết định zoom
            Camera.main.orthographicSize = (config.gridHeight / 2f) + padding;
        }
        else
        {
            // Màn hình hẹp hơn lưới -> chiều rộng quyết định zoom
            float newOrthographicSize = (config.gridWidth / 2f) + padding;
            Camera.main.orthographicSize = newOrthographicSize / screenRatio;
        }

        // 3. Cập nhật vị trí và kích thước của Background và Frame
        if (backgroundRenderer != null)
        {
            // Đặt background vào tâm lưới
            backgroundRenderer.transform.position = new Vector3(gridCenterX, gridCenterY, 0);
            // Co giãn background để vừa với lưới (thêm một chút viền)
            backgroundRenderer.size = new Vector2(config.gridWidth + padding, config.gridHeight + padding);
        }

        if (frameRenderer != null)
        {
            // Đặt frame vào tâm lưới
            frameRenderer.transform.position = new Vector3(gridCenterX, gridCenterY, 0);
            // Co giãn frame (vì là Sliced mode nên sẽ co giãn đẹp)
            frameRenderer.size = new Vector2(config.gridWidth + padding, config.gridHeight + padding);
        }
    }
}