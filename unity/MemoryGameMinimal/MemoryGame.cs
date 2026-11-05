// MemoryGame.cs
// ============================================================================
// 记忆游戏最小原型 —— 序列展示 / 输入判定 / 成功提示
// 挂到一个空物体（如 GameManager）上，并把 Player 与 3 个 Node 拖进字段。
//
// 玩法：先看节点按序列高亮（展示阶段），再用键盘把小球开到节点，
//       按展示顺序依次到达即判定成功。错一步则本序列重来。
// 延申：事件打点 -> 经 UDP 回传 Python（见 OnSequenceComplete / LogEvent）。
// ============================================================================

using UnityEngine;

public class MemoryGame : MonoBehaviour
{
    public Transform player;
    public Transform[] nodes;          // 顺序无所谓，用数组下标作"节点编号"

    [Header("参数")]
    public float reachRadius = 0.8f;   // 判定"到达节点"的距离
    public float showInterval = 0.8f;  // 展示阶段每个节点高亮时长
    public float showGap = 0.3f;       // 节点间停顿
    public int sequenceLength = 3;     // 序列长度（难度参数）

    // ---------- 内部状态 ----------
    private enum Phase { Idle, Show, Input, Success }
    private Phase phase = Phase.Idle;

    private int[] sequence;            // 目标序列（节点下标）
    private int stepIndex = 0;         // 当前已正确到达的步数
    private bool canRegister = true;   // 是否允许登记下一步

    private float showTimer = 0f;
    private int showPos = 0;
    private Vector3[] nodeBaseScale;

    // 用于记录反应时
    private float inputStartTime = 0f;

    void Start()
    {
        nodeBaseScale = new Vector3[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
            nodeBaseScale[i] = nodes[i].localScale;

        NewRound();
    }

    void NewRound()
    {
        // 生成随机目标序列
        sequence = new int[Mathf.Min(sequenceLength, nodes.Length)];
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = Random.Range(0, nodes.Length);

        stepIndex = 0;
        canRegister = true;
        showPos = 0;
        showTimer = 0f;
        phase = Phase.Show;

        LogEvent("TASK_START", "task_id=4");
        LogEvent("SEQUENCE_SHOW", "target=" + SeqToString(sequence));
    }

    void Update()
    {
        if (phase == Phase.Show)
            RunShow();
        else if (phase == Phase.Input)
            RunInput();

        // 按 R 重开一局（便于测试不同序列）
        if (Input.GetKeyDown(KeyCode.R))
            NewRound();

        // 成功后按任意键再来一局
        if (phase == Phase.Success && Input.anyKeyDown)
            NewRound();
    }

    // ---------- 展示阶段：依次高亮节点 ----------
    void RunShow()
    {
        showTimer += Time.deltaTime;
        int cur = sequence[showPos];
        Highlight(cur, true);

        if (showTimer >= showInterval)
        {
            Highlight(cur, false);
            showTimer = -showGap; // 停顿
            showPos++;
            if (showPos >= sequence.Length)
            {
                phase = Phase.Input;
                inputStartTime = Time.time;
                LogEvent("ACTION_START", "task_id=4");
            }
        }
    }

    // ---------- 输入阶段：检测小球到达节点并判定 ----------
    void RunInput()
    {
        bool nearAny = false;
        for (int i = 0; i < nodes.Length; i++)
        {
            float d = Vector3.Distance(player.position, nodes[i].position);
            if (d <= reachRadius)
            {
                nearAny = true;
                if (canRegister)
                {
                    canRegister = false;
                    if (i == sequence[stepIndex])
                    {
                        stepIndex++;
                        LogEvent("STEP_CORRECT", $"step={stepIndex}/{sequence.Length} node={i}");
                        if (stepIndex >= sequence.Length)
                            OnSequenceComplete();
                    }
                    else
                    {
                        LogEvent("STEP_WRONG", $"expected={sequence[stepIndex]} got={i}");
                        stepIndex = 0; // 本序列重来
                    }
                }
            }
        }
        if (!nearAny) canRegister = true; // 离开节点后才允许登记下一步
    }

    void OnSequenceComplete()
    {
        phase = Phase.Success;
        float rt = (Time.time - inputStartTime) * 1000f;
        LogEvent("SEQUENCE_COMPLETE",
                 $"task_id=4 is_correct=True reaction_time_ms={rt:F0}");
        LogEvent("TASK_SUCCESS", "task_id=4");
    }

    // ---------- 工具 ----------
    void Highlight(int idx, bool on)
    {
        if (idx < 0 || idx >= nodes.Length) return;
        nodes[idx].localScale = on ? nodeBaseScale[idx] * 1.6f : nodeBaseScale[idx];
    }

    string SeqToString(int[] s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append("节点").Append(s[i]);
            if (i < s.Length - 1) sb.Append("→");
        }
        return sb.ToString();
    }

    void LogEvent(string gameEvent, string extra)
    {
        // 最小原型：先打 Console。正式版改为经 UDP 回传 Python，
        // 写入 game_event / task_id / trial_id / target / user_response / is_correct / reaction_time_ms
        Debug.Log($"[MemoryGame] event={gameEvent} | {extra}");
    }

    // ---------- 画面提示（无需 Canvas，OnGUI 最简）----------
    void OnGUI()
    {
        GUIStyle box = new GUIStyle(GUI.skin.box);
        box.fontSize = 16; box.normal.textColor = Color.white;

        string msg = "";
        if (phase == Phase.Show)
            msg = "记住顺序：" + SeqToString(sequence) + "\n（请观察高亮节点）";
        else if (phase == Phase.Input)
            msg = "按顺序开到节点！\n进度：" + stepIndex + "/" + sequence.Length
                + "\n目标：" + SeqToString(sequence);
        else if (phase == Phase.Success)
            msg = "🎉 完成！魔法森林亮了！\n按任意键再来一局（R 换新序列）";

        GUI.Box(new Rect(20, 20, 360, 90), msg, box);
        GUI.Label(new Rect(20, 120, 400, 20),
            "键盘 A/D=左右  W/S=前后   |   R=重开");
    }
}
