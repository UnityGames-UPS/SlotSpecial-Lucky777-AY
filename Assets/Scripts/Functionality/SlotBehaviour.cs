using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System;

public class SlotBehaviour : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField]
    internal Sprite[] myImages;  //images taken initially

    [Header("Slot Images")]
    [SerializeField]
    private List<SlotImage> images;     //class to store total images
    [SerializeField]
    internal List<SlotImage> Tempimages;     //class to store the result matrix

    [Header("Slots Elements")]
    [SerializeField]
    private LayoutElement[] Slot_Elements;

    [Header("Slots Transforms")]
    [SerializeField]
    private Transform[] Slot_Transform;

    [Header("Line Button Objects")]
    [SerializeField]
    private List<GameObject> StaticLine_Objects;

    [Header("Line Button Texts")]
    [SerializeField]
    private List<TMP_Text> StaticLine_Texts;

    private Dictionary<int, string> y_string = new Dictionary<int, string>();

    [Header("Buttons")]
    [SerializeField]
    private Button SlotStart_Button;
    [SerializeField]
    private Button AutoSpin_Button;
    [SerializeField] private Button AutoSpinStop_Button;
    [SerializeField]
    private Button MaxBet_Button;
    [SerializeField]
    private Button TBetPlus_Button;
    [SerializeField]
    private Button TBetMinus_Button;
    [SerializeField] private Button Turbo_Button;
    [SerializeField] private Button StopSpin_Button;

    [Header("Animated Sprites")]
    [SerializeField]
    private Sprite[] seven_blue;
    [SerializeField]
    private Sprite[] seven_orange;
    [SerializeField]
    private Sprite[] seven_white;
    [SerializeField]
    private Sprite[] bar_orange;
    [SerializeField]
    private Sprite[] bar_white;
    [SerializeField]
    private Sprite[] bar_blue;
    [SerializeField]
    private Sprite[] wild_seven;


    [Header("Miscellaneous UI")]
    [SerializeField]
    private TMP_Text Balance_text;
    [SerializeField]
    private TMP_Text TotalBet_text;
    [SerializeField]
    private TMP_Text LineBet_text;
    [SerializeField]
    private TMP_Text TotalWin_text;

    [Header("Audio Management")]
    [SerializeField]
    private AudioController audioController;

    [SerializeField]
    private UIManager uiManager;


    [Header("Free Spins Board")]
    [SerializeField]
    private GameObject FSBoard_Object;
    [SerializeField]
    private TMP_Text FSnum_text;

    int tweenHeight = 0;  //calculate the height at which tweening is done

    [SerializeField]
    private GameObject Image_Prefab;    //icons prefab
    [SerializeField] Sprite[] TurboToggleSprites;
    [SerializeField]
    private PayoutCalculation PayCalculator;

    private List<Tweener> alltweens = new List<Tweener>();

    private Tweener WinTween = null;

    [SerializeField]
    private List<ImageAnimation> TempList;  //stores the sprites whose animation is running at present 

    [SerializeField]
    private SocketIOManager SocketManager;

    private Coroutine AutoSpinRoutine = null;
    private Coroutine FreeSpinRoutine = null;
    private Coroutine tweenroutine;
    private Tween BalanceTween;
    internal bool IsAutoSpin = false;
    private bool IsSpinning = false;
    private bool CheckSpinAudio = false;
    internal bool CheckPopups = false;
    internal int BetCounter = 0;
    private double currentBalance = 0;
    private double currentTotalBet = 0;
    protected int Lines = 9;
    [SerializeField]
    private int IconSizeFactor = 100;       //set this parameter according to the size of the icon and spacing
    private int numberOfSlots = 3;          //number of columns
    private bool StopSpinToggle;
    private float SpinDelay = 0.2f;
    internal bool IsTurboOn;
    internal bool WasAutoSpinOn;
    internal bool IsHoldSpin;
    internal bool LinesAnimDone;
    internal bool skipImageAnimation;
    [SerializeField]
    private ImageAnimation winTableAnimation;

    private void Start()
    {
        IsAutoSpin = false;

        if (SlotStart_Button) SlotStart_Button.onClick.RemoveAllListeners();
        if (SlotStart_Button) SlotStart_Button.onClick.AddListener(delegate { StartSlots(); });

        if (TBetPlus_Button) TBetPlus_Button.onClick.RemoveAllListeners();
        if (TBetPlus_Button) TBetPlus_Button.onClick.AddListener(delegate { ChangeBet(true); });

        if (TBetMinus_Button) TBetMinus_Button.onClick.RemoveAllListeners();
        if (TBetMinus_Button) TBetMinus_Button.onClick.AddListener(delegate { ChangeBet(false); });

        if (MaxBet_Button) MaxBet_Button.onClick.RemoveAllListeners();
        if (MaxBet_Button) MaxBet_Button.onClick.AddListener(MaxBet);

        if (StopSpin_Button) StopSpin_Button.onClick.RemoveAllListeners();
        if (StopSpin_Button) StopSpin_Button.onClick.AddListener(() => { audioController.PlayButtonAudio(); StopSpinToggle = true; StopSpin_Button.gameObject.SetActive(false); });

        if (AutoSpin_Button) AutoSpin_Button.onClick.RemoveAllListeners();
        if (AutoSpin_Button) AutoSpin_Button.onClick.AddListener(AutoSpin);

        if (Turbo_Button) Turbo_Button.onClick.RemoveAllListeners();
        if (Turbo_Button) Turbo_Button.onClick.AddListener(TurboToggle);

        if (AutoSpinStop_Button) AutoSpinStop_Button.onClick.RemoveAllListeners();
        if (AutoSpinStop_Button) AutoSpinStop_Button.onClick.AddListener(StopAutoSpin);

        if (FSBoard_Object) FSBoard_Object.SetActive(false);

        tweenHeight = (9 * IconSizeFactor) - 280;
        Debug.Log(tweenHeight);
    }

    internal void StartSpinRoutine()
    {
        if (!IsSpinning)
        {
            IsHoldSpin = false;
            Invoke("AutoSpinHold", 2f);
        }
    }

    internal void StopSpinRoutine()
    {
        CancelInvoke("AutoSpinHold");
        if (IsAutoSpin)
        {
            IsAutoSpin = false;
            if (AutoSpinStop_Button) AutoSpinStop_Button.gameObject.SetActive(false);
            StartCoroutine(StopAutoSpinCoroutine());
        }
    }

    private void AutoSpinHold()
    {
        Debug.Log("Auto Spin Started");
        IsHoldSpin = true;
        AutoSpin();
    }



    void TurboToggle()
    {
        audioController.PlayButtonAudio();
        if (IsTurboOn)
        {
            IsTurboOn = false;
            Turbo_Button.GetComponent<ImageAnimation>().StopAnimation();
            Turbo_Button.image.sprite = TurboToggleSprites[0];
            Turbo_Button.image.color = new Color(0.86f, 0.86f, 0.86f, 1);
        }
        else
        {
            IsTurboOn = true;
            Turbo_Button.GetComponent<ImageAnimation>().StartAnimation();
            Turbo_Button.image.color = new Color(1, 1, 1, 1);
        }
    }

    #region Autospin
    private void AutoSpin()
    {
        if (!IsAutoSpin)
        {

            IsAutoSpin = true;
            if (AutoSpinStop_Button) AutoSpinStop_Button.gameObject.SetActive(true);
            if (AutoSpin_Button) AutoSpin_Button.gameObject.SetActive(false);

            if (AutoSpinRoutine != null)
            {
                StopCoroutine(AutoSpinRoutine);
                AutoSpinRoutine = null;
            }
            AutoSpinRoutine = StartCoroutine(AutoSpinCoroutine());

        }
    }

    private void StopAutoSpin()
    {
        audioController.PlayButtonAudio();
        if (IsAutoSpin)
        {
            IsAutoSpin = false;
            if (AutoSpinStop_Button) AutoSpinStop_Button.gameObject.SetActive(false);
            if (AutoSpin_Button) AutoSpin_Button.gameObject.SetActive(true);
            StartCoroutine(StopAutoSpinCoroutine());
        }
    }

    private IEnumerator AutoSpinCoroutine()
    {
        while (IsAutoSpin)
        {
            StartSlots(IsAutoSpin);
            yield return tweenroutine;
            yield return new WaitForSeconds(SpinDelay);
        }
        WasAutoSpinOn = false;
    }

    private IEnumerator StopAutoSpinCoroutine()
    {
        yield return new WaitUntil(() => !IsSpinning);
        ToggleButtonGrp(true);
        if (AutoSpinRoutine != null || tweenroutine != null)
        {
            StopCoroutine(AutoSpinRoutine);
            StopCoroutine(tweenroutine);
            tweenroutine = null;
            AutoSpinRoutine = null;
            StopCoroutine(StopAutoSpinCoroutine());
        }
    }
    #endregion


    private void CompareBalance()
    {
        Debug.Log(currentTotalBet);
        if (currentBalance < currentTotalBet)
        {
            uiManager.LowBalPopup();
        }
    }

    #region LinesCalculation
    //Fetch Lines from backend
    internal void FetchLines(string LineVal, int count)
    {
        y_string.Add(count + 1, LineVal);
        StaticLine_Texts[count].text = (count + 1).ToString();
        StaticLine_Objects[count].SetActive(true);
    }

    //Generate Static Lines from button hovers
    internal void GenerateStaticLine(TMP_Text LineID_Text)
    {
        DestroyStaticLine();
        int LineID = 1;
        try
        {
            LineID = int.Parse(LineID_Text.text);
        }
        catch (Exception e)
        {
            Debug.Log("Exception while parsing " + e.Message);
        }
        List<int> y_points = null;
        y_points = y_string[LineID]?.Split(',')?.Select(Int32.Parse)?.ToList();
        //  PayCalculator.GeneratePayoutLinesBackend(y_points, y_points.Count, true);
    }

    //Destroy Static Lines from button hovers
    internal void DestroyStaticLine()
    {
        PayCalculator.ResetStaticLine();
    }
    #endregion

    private void MaxBet()
    {
        if (audioController) audioController.PlayButtonAudio();
        BetCounter = SocketManager.initialData.bets.Count - 1;
        if (LineBet_text) LineBet_text.text = SocketManager.initialData.bets[BetCounter].ToString();
        if (TotalBet_text) TotalBet_text.text = (SocketManager.initialData.bets[BetCounter] * Lines).ToString();
        currentTotalBet = SocketManager.initialData.bets[BetCounter] * Lines;

    }

    private void ChangeBet(bool IncDec)
    {
        if (audioController) audioController.PlayButtonAudio();
        if (IncDec)
        {
            BetCounter++;
            if (BetCounter >= SocketManager.initialData.bets.Count)
            {
                BetCounter = 0; // Loop back to the first bet
            }
        }
        else
        {
            BetCounter--;
            if (BetCounter < 0)
            {
                BetCounter = SocketManager.initialData.bets.Count - 1; // Loop to the last bet
            }
        }
        if (LineBet_text) LineBet_text.text = SocketManager.initialData.bets[BetCounter].ToString();
        if (TotalBet_text) TotalBet_text.text = (SocketManager.initialData.bets[BetCounter] * Lines).ToString();
        currentTotalBet = SocketManager.initialData.bets[BetCounter] * Lines;

    }

    #region InitialFunctions
    internal void shuffleInitialMatrix()
    {
        for (int i = 0; i < Tempimages.Count; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int randomIndex = UnityEngine.Random.Range(0, myImages.Length);
                Tempimages[i].slotImages[j].sprite = myImages[randomIndex];
            }
        }
    }

    internal void SetInitialUI()
    {
        BetCounter = 0;
        if (LineBet_text) LineBet_text.text = SocketManager.initialData.bets[BetCounter].ToString();
        if (TotalBet_text) TotalBet_text.text = (SocketManager.initialData.bets[BetCounter] * Lines).ToString();
        if (TotalWin_text) TotalWin_text.text = "0.000";
        if (Balance_text) Balance_text.text = SocketManager.playerdata.balance.ToString("F3");
        currentBalance = SocketManager.playerdata.balance;
        currentTotalBet = SocketManager.initialData.bets[BetCounter] * Lines;
        CompareBalance();
        uiManager.InitialiseUIData(SocketManager.initUIData.paylines);
    }
    #endregion

    private void OnApplicationFocus(bool focus)
    {
        audioController.CheckFocusFunction(focus, CheckSpinAudio);
    }

    //function to populate animation sprites accordingly
    private void PopulateAnimationSprites(ImageAnimation animScript, int val)
    {
        animScript.textureArray.Clear();
        animScript.textureArray.TrimExcess();
        switch (val)
        {
            case 0:
                for (int i = 0; i < seven_orange.Length; i++)
                {
                    animScript.textureArray.Add(seven_orange[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
            case 1:
                for (int i = 0; i < seven_white.Length; i++)
                {
                    animScript.textureArray.Add(seven_white[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
            case 2:
                for (int i = 0; i < seven_blue.Length; i++)
                {
                    animScript.textureArray.Add(seven_blue[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
            case 3:
                for (int i = 0; i < bar_orange.Length; i++)
                {
                    animScript.textureArray.Add(bar_orange[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
            case 4:
                for (int i = 0; i < bar_white.Length; i++)
                {
                    animScript.textureArray.Add(bar_white[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
            case 5:
                for (int i = 0; i < bar_blue.Length; i++)
                {
                    animScript.textureArray.Add(bar_blue[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
            case 6:
                for (int i = 0; i < wild_seven.Length; i++)
                {
                    animScript.textureArray.Add(wild_seven[i]);
                }
                animScript.AnimationSpeed = 24f;
                break;
        }
    }

    #region SlotSpin
    //starts the spin process
    private void StartSlots(bool autoSpin = false)
    {
        if (audioController) audioController.PlaySpinButtonAudio();

        if (!autoSpin)
        {
            if (AutoSpinRoutine != null)
            {
                StopCoroutine(AutoSpinRoutine);
                StopCoroutine(tweenroutine);
                tweenroutine = null;
                AutoSpinRoutine = null;
            }
        }
        WinningsAnim(false);
        if (SlotStart_Button) SlotStart_Button.interactable = false;
        if (TempList.Count > 0)
        {
            StopGameAnimation();
        }
        PayCalculator.ResetLines();
        tweenroutine = StartCoroutine(TweenRoutine());
    }

    //manage the Routine for spinning of the slots
    private IEnumerator TweenRoutine()
    {
        Debug.Log(currentTotalBet);
        if (currentBalance < currentTotalBet)
        {
            CompareBalance();
            StopAutoSpin();
            yield return new WaitForSeconds(1);
            ToggleButtonGrp(true);
            yield break;
        }
        TotalWin_text.text = 0.ToString("f3");
        if (audioController) audioController.PlayWLAudio("spin");
        CheckSpinAudio = true;

        IsSpinning = true;
        winTableAnimation.gameObject.SetActive(false);
        ToggleButtonGrp(false);
        if (!IsTurboOn && !IsAutoSpin)
        {
            StopSpin_Button.gameObject.SetActive(true);
        }
        for (int i = 0; i < numberOfSlots; i++)
        {

            InitializeTweening(Slot_Transform[i]);
            yield return new WaitForSeconds(0.1f);
        }
        BalanceDeduction();
        SocketManager.AccumulateResult(BetCounter);
        yield return new WaitUntil(() => SocketManager.isResultdone);

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                // Debug.Log(SocketManager.resultData)
                int resultNum = int.Parse(SocketManager.resultData.matrix[i][j]);

                PopulateAnimationSprites(Tempimages[j].slotImages[i].GetComponent<ImageAnimation>(), resultNum);
                Tempimages[j].slotImages[i].GetComponent<Image>().sprite = myImages[resultNum];
            }
        }
        if (IsTurboOn)
        {
            StopSpinToggle = true;
            yield return new WaitForSeconds(0.1f);
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.1f);
                if (StopSpinToggle)
                {
                    break;
                }
            }
            StopSpin_Button.gameObject.SetActive(false);
        }

        for (int i = 0; i < numberOfSlots; i++)
        {
            yield return StopTweening(2, Slot_Transform[i], i, StopSpinToggle);
        }
        StopSpinToggle = false;

        yield return alltweens[^1].WaitForCompletion();
        KillAllTweens();

        if (SocketManager.resultData.payload.winAmount > 0)
        {
            SpinDelay = 1.2f;
        }
        else
        {
            SpinDelay = 0.2f;
        }
        if (SocketManager.resultData.payload.winAmount > 0)
        {
            // List<int> winLine = new();
            // foreach (var item in SocketManager.resultData.payload.lineWins)
            // {
            //     winLine.Add(item.lineIndex);
            // }
            Debug.Log("Dev Test : " + SocketManager.resultData.payload.winAmount);
            StartCoroutine(CheckPayoutLineBackend(SocketManager.resultData.payload.lineWins));

        }
        else
        {
            if (audioController) audioController.StopWLAaudio();
        }

        CheckPopups = true;

        if (TotalWin_text) TotalWin_text.text = SocketManager.resultData.payload.winAmount.ToString("F3");
        BalanceTween?.Kill();
        if (Balance_text) Balance_text.text = SocketManager.playerdata.balance.ToString("F3");

        currentBalance = SocketManager.playerdata.balance;

        if (SocketManager.resultData.features.isJackpot)
        {
            uiManager.PopulateWin(4, SocketManager.resultData.payload.winAmount);
            yield return new WaitUntil(() => !CheckPopups);
            CheckPopups = true;
        }
        CheckWinPopups();
        yield return new WaitUntil(() => !CheckPopups);
        if (!IsAutoSpin)
        {
            ToggleButtonGrp(true);
            IsSpinning = false;
        }
        else
        {
            yield return new WaitForSeconds(1f);
            IsSpinning = false;
        }


    }

    private void BalanceDeduction()
    {
        double bet = 0;
        double balance = 0;
        try
        {
            bet = double.Parse(TotalBet_text.text);
        }
        catch (Exception e)
        {
            Debug.Log("Error while conversion " + e.Message);
        }

        try
        {
            balance = double.Parse(Balance_text.text);
        }
        catch (Exception e)
        {
            Debug.Log("Error while conversion " + e.Message);
        }
        double initAmount = balance;
        Debug.Log(balance + "  " + bet);
        balance = balance - bet;
        if (Balance_text) Balance_text.text = balance.ToString("f3");

    }

    internal void CheckWinPopups()
    {
        CheckPopups = false;
    }

    public static List<int> ExtractFirstNumbers(List<string> positions)
    {
        List<int> result = new List<int>();

        foreach (string pos in positions)
        {
            if (!string.IsNullOrEmpty(pos) && pos.Contains(","))
            {
                string[] parts = pos.Split(',');
                if (int.TryParse(parts[0], out int number))
                {
                    result.Add(number);
                }
            }
        }

        return result;
    }
    //generate the payout lines generated 
    private IEnumerator CheckPayoutLineBackend(List<LineWin> winLines, double jackpot = 0)
    {
        Debug.Log("Dev Test : " + winLines + ".       ." + winLines);
        List<int> y_points = null;
        List<int> points_anim = null;
        if (winLines.Count > 0)
        {
            if (jackpot <= 0)
            {
                if (audioController) audioController.PlayWLAudio("win");
            }

            winTableAnimation.gameObject.SetActive(true);
            winTableAnimation.StartAnimation();
            skipImageAnimation = false;
            LinesAnimDone = false;
            for (int i = 0; i < winLines.Count; i++)
            {
                List<int> resSymb = new List<int>();
                for (int j = 0; j < winLines[i].positions.Count; j++)
                {
                    int resultNum = ExtractFirstNumbers(winLines[i].positions)[j];
                    //  int resultNum = y_points = y_string[LineId[i] + 1]?.Split(',')?.Select(Int32.Parse)?.ToList(); ;
                    resSymb.Add(resultNum);
                }

                PayCalculator.generateLinesAnim(resSymb);
            }

            yield return new WaitUntil(() => LinesAnimDone);
            // if (!skipImageAnimation)
            // {
            //     for (int i = 0; i < points_AnimString.Count; i++)
            //     {
            //         points_anim = points_AnimString[i]?.Split(',')?.Select(Int32.Parse)?.ToList();

            //         for (int k = 0; k < points_anim.Count; k++)
            //         {
            //             if (points_anim[k] >= 10)
            //             {
            //                 StartGameAnimation(Tempimages[(points_anim[k] / 10) % 10].slotImages[points_anim[k] % 10].gameObject);
            //             }
            //             else
            //             {
            //                 StartGameAnimation(Tempimages[0].slotImages[points_anim[k]].gameObject);
            //             }
            //         }
            //     }
            if (!skipImageAnimation)
            {
                List<KeyValuePair<int, int>> coords = new();
                for (int j = 0; j < winLines.Count; j++)
                {
                    for (int k = 0; k < winLines[j].positions.Count; k++)
                    {
                        int rowIndex = SocketManager.initialData.lines[winLines[j].lineIndex][k];
                        int columnIndex = k;
                        coords.Add(new KeyValuePair<int, int>(rowIndex, columnIndex));
                    }
                }

                foreach (var coord in coords)
                {
                    int rowIndex = coord.Key;
                    int columnIndex = coord.Value;
                    //   Debug.Log("Dev Test : " + rowIndex + ".       ." + columnIndex);
                    StartGameAnimation(Tempimages[columnIndex].slotImages[rowIndex].gameObject);
                }

                WinningsAnim(true);
            }
        }
        else
        {
            if (audioController) audioController.StopWLAaudio();
        }
        CheckSpinAudio = false;
        yield return null;
    }


    internal void playwinAnimations()
    {

    }

    private void WinningsAnim(bool IsStart)
    {
        if (IsStart)
        {
            WinTween = TotalWin_text.gameObject.GetComponent<RectTransform>().DOScale(new Vector2(1.5f, 1.5f), 1f).SetLoops(-1, LoopType.Yoyo).SetDelay(0);
        }
        else
        {
            WinTween.Kill();
            TotalWin_text.gameObject.GetComponent<RectTransform>().localScale = Vector3.one;
        }
    }

    #endregion

    internal void CallCloseSocket()
    {
        StartCoroutine(SocketManager.CloseSocket());
    }


    void ToggleButtonGrp(bool toggle)
    {
        if (SlotStart_Button) SlotStart_Button.interactable = toggle;
        if (MaxBet_Button) MaxBet_Button.interactable = toggle;
        if (AutoSpin_Button) AutoSpin_Button.interactable = toggle;
        if (TBetMinus_Button) TBetMinus_Button.interactable = toggle;
        if (TBetPlus_Button) TBetPlus_Button.interactable = toggle;
        // if(Turbo_Button) Turbo_Button.interactable = toggle;
    }

    //start the icons animation
    private void StartGameAnimation(GameObject animObjects)
    {
        ImageAnimation temp = animObjects.GetComponent<ImageAnimation>();
        temp.StartAnimation();
        TempList.Add(temp);
    }

    //stop the icons animation
    internal void StopGameAnimation()
    {
        for (int i = 0; i < TempList.Count; i++)
        {
            TempList[i].StopAnimation();
        }
        TempList.Clear();
        TempList.TrimExcess();
    }


    #region TweeningCode
    private void InitializeTweening(Transform slotTransform)
    {
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, 0);
        Tweener tweener = slotTransform.DOLocalMoveY(-tweenHeight, 0.2f).SetLoops(-1, LoopType.Restart).SetDelay(0);
        tweener.Play();
        alltweens.Add(tweener);
    }



    private IEnumerator StopTweening(int reqpos, Transform slotTransform, int index, bool isStop)
    {
        alltweens[index].Kill();
        int tweenpos = (reqpos * IconSizeFactor) - IconSizeFactor;
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, 0);
        alltweens[index] = slotTransform.DOLocalMoveY(-tweenpos + 100, 0.5f).SetEase(Ease.OutElastic);
        if (!isStop)
        {
            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            yield return null;
        }
    }


    private void KillAllTweens()
    {
        for (int i = 0; i < numberOfSlots; i++)
        {
            alltweens[i].Kill();
        }
        alltweens.Clear();

    }
    #endregion

}

[Serializable]
public class SlotImage
{
    public List<Image> slotImages = new List<Image>(10);
}

