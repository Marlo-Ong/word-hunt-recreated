using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class Main : MonoBehaviour
{
    // Game Settings
    [SerializeField] private int gameTimeLimitSeconds;
    [SerializeField] private int xBounds;
    [SerializeField] private int yBounds;

    // Game Object References
    [SerializeField] private AudioSource musicSource, fxSource;
    [SerializeField] private List<AudioClip> sfx;
    [SerializeField] private Tilemap tiles;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject lineObject;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI wordCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Button replayButton;
    [SerializeField] private AssetReference _addressableTextAsset = null;
    private LineRenderer chainLine;

    // Script Variables
    Dictionary<string, int> validWords;
    Dictionary<string, int> wordsSpelled;
    string word;
    int totalScore, wordCount;
    float timer;
    Color baseColor;
    HashSet<GameObject> usedTiles;
    char[,] grid;
    GameObject[,] tileGrid;
    bool gameOver;
    Vector3Int prevCell;

    void Start()
    {
        baseColor = new Color(1.0f, 0.85f, 0.59f);
        validWords = new Dictionary<string, int>();
        wordsSpelled = new Dictionary<string, int>();
        usedTiles = new HashSet<GameObject>();
        tileGrid = new GameObject[xBounds, yBounds];
        grid = new char[xBounds, yBounds];
        chainLine = lineObject.GetComponent<LineRenderer>();

        resetVars();
        createTiles();
        fillGrid();

        // Use addressing to access CSM19.txt asynchronously
        _addressableTextAsset.LoadAssetAsync<TextAsset>().Completed += handle =>
        {
            // Load words by line into dictionary
            string[] lines = handle.Result.text.Split("\n");
            foreach (string word in lines) {
                string w = word.Trim();
                if (w.Length >= 3) {
                    validWords.Add(w, Score(w.Length));
                }
            }
            Addressables.Release(handle);
        };

        StartCoroutine(gameTimer());
    }

    void Update()
    {
        if (!gameOver){

            // Timer functions
            timer -= Time.deltaTime;
            updateTime(timer);

            // Tap finger, start new word
            if (Input.GetMouseButtonDown(0))
            {   
                word = "";
                usedTiles.Clear();
            }

            // Move finger, add letter
            if(Input.GetMouseButton(0))
            {
                Vector2 worldPt = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector3Int curCell = tiles.WorldToCell(worldPt);

                // Pre-validate word
                if (withinBounds(curCell) && (word.Length == 0 || isAdjacent(curCell, prevCell))){ // If not first letter, next tile must be adjacent
                    GameObject curTile = tileGrid[curCell.x, curCell.y];

                    if (!usedTiles.Contains(curTile)) {
                        usedTiles.Add(curTile);
                        prevCell = curCell;

                        // VFX: Embiggen selected letter
                        float lerp = 1.15f;
                        curTile.transform.localScale = new Vector3(lerp, lerp, lerp);

                        // Add letter to word
                        char letter = grid[curCell.x, curCell.y];
                        word += letter;
                    
                        // Play appropriate SFX and tint tiles appropriately
                        if (validWords.ContainsKey(word)){
                            if (wordsSpelled.ContainsKey(word)){ // Already spelled word
                                fxSource.PlayOneShot(sfx[0]);
                                colorUsedTiles(Color.yellow);
                                colorLine(Color.white);
                            }
                            else { // Newly spelled word
                                fxSource.PlayOneShot(sfx[2]);
                                colorUsedTiles(Color.green);
                                colorLine(Color.white);
                            }
                        }
                        else { // Invalid word
                            fxSource.PlayOneShot(sfx[0]);
                            colorUsedTiles(Color.white);
                            colorLine(Color.red);
                        }

                        // Move chain line 
                        lineObject.SetActive(true);
                        Vector3 pos = worldPtToCentered(tiles.CellToWorld(curCell));           
                        for (int i = word.Length-1; i < 16; i++){
                            chainLine.SetPosition(i, pos);
                        }
                    }
                }
            }

            // Release finger, validate word
            if (Input.GetMouseButtonUp(0))
            {
                int fx_toplay = 1; // default "pop" sound
                word = word.ToUpper();

                // Reset tiles and line renderer
                colorUsedTiles(baseColor);
                descaleUsedTiles();
                lineObject.SetActive(false);

                if (word.Length >= 2){ // Quirk: Words with < 3 letters are not valid, but 2-letter words still make SFX
                    if (validateWord(word)) {
                        wordsSpelled.Add(word, validWords[word]);
                        totalScore += validWords[word];
                        wordCount++;

                        // Update score board and SFX
                        scoreText.text = totalScore.ToString();
                        wordCountText.text = wordCount.ToString();
                        fx_toplay = word.Length > 6 ? 6 : word.Length;
                    }

                    fxSource.PlayOneShot(sfx[fx_toplay]);
                }
            }
        }
    }

    private int Score(int wordLength){
        if (wordLength == 3) return 100;
        if (wordLength == 4) return 400;
        if (wordLength == 5) return 800;
        return wordLength * 400 - 1000;
    }

    private bool validateWord(string word){
        return validWords.ContainsKey(word) && !wordsSpelled.ContainsKey(word);
    }

    private void colorUsedTiles(Color newColor){
        foreach (GameObject tile in usedTiles){
            tile.GetComponent<SpriteRenderer>().color = newColor;
        }
    }

    private void descaleUsedTiles(){
        foreach (GameObject tile in usedTiles){
            tile.transform.localScale = new Vector3(1, 1, 1);
        }
    }

    private bool withinBounds(Vector3Int cell) {
        return cell.x >= 0 && cell.y >= 0 && cell.x < xBounds && cell.y < yBounds;
    }

    private Vector3 worldPtToCentered(Vector3 worldPos){
        Vector3 pos = worldPos;
        pos.x += 7f;
        pos.y += 6f;
        return pos;
    }

    private void colorLine(Color newColor) {
        chainLine.startColor = newColor;
        chainLine.endColor = newColor;
    }

    private void fillGrid(){
        // Fill tiles with letters
        string charsDistributed = "EEEEEEEEEEEEAAAAAAAAAIIIIIIIIIOOOOOOOONNNNNNRRRRRRTTTTTTLLLLSSSSUUUUDDDDGGGBBCCMMPPFFHHVVWWYYKJXQZ";
        
        for (int i = 0; i < xBounds; i++) {
            for (int j = 0; j < yBounds; j++) {

                // Get random letter and change corresponding UI text
                int randInd = Random.Range(0, charsDistributed.Length);
                char randLetter = charsDistributed[randInd];
                grid[i,j] = randLetter;
                tileGrid[i,j].transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = randLetter.ToString();
                charsDistributed = charsDistributed.Remove(randInd, 1);
                
            }
        }
    }

    private void createTiles(){
        // Create grid of tiles in game
        
        for (int i = 0; i < xBounds; i++) {
            for (int j = 0; j < yBounds; j++) {

                // Create tile sprite
                Vector3Int loc = new Vector3Int(i,j,0);
                GameObject newTile = Instantiate(tilePrefab, tiles.CellToWorld(loc), Quaternion.identity);
                newTile.transform.parent = tilePrefab.transform.parent;
                tileGrid[i,j] = newTile;
            }
        }
    }

    private IEnumerator gameTimer() {
        yield return new WaitForSeconds(gameTimeLimitSeconds - 5f);
        fxSource.PlayOneShot(sfx[9]);
        yield return new WaitForSeconds(5f);
        gameOver = true;
        replayButton.gameObject.SetActive(true);
        fxSource.PlayOneShot(sfx[10]);
    }

    public void replayGame() {
        replayButton.gameObject.SetActive(false);
        fxSource.PlayOneShot(sfx[8]);
        fillGrid();
        resetVars();
        StartCoroutine(gameTimer());
    }

    private void resetVars() {
        wordCount = 0;
        wordCountText.text = "0";
        totalScore = 0;
        scoreText.text = "0000";
        timer = gameTimeLimitSeconds + 1f;
        wordsSpelled.Clear();
        gameOver = false;
    }

    private void updateTime(float timeToDisplay) {
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);  
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private bool isAdjacent(Vector3Int a, Vector3Int b) {
        return Mathf.Abs(b.x - a.x) <= 1 && Mathf.Abs(b.y - a.y) <= 1 && Mathf.Abs(b.z - a.z) <= 1;
    }
}