using System.Text;
using UnityEngine;

/// <summary>
/// BANC DE MESURE (soutenance) — les 3 IA tournent EN MÊME TEMPS.
///
/// Ce script ne décide RIEN : il observe et il compte.
/// Il couvre LES 5 MÉTRIQUES demandées par le plan de soutenance :
///
///   1. ⏱️  TEMPS        : moyenne par décision + cumul par partie
///   2. 🎯  PERFORMANCE  : réussite, score, OPTIMALITÉ du trajet
///   3. 💾  MÉMOIRE      : nœuds explorés, nœuds d'arbre, taille des structures
///   4. 🔄  RÉACTIVITÉ   : délai entre deux décisions (ms) = vitesse de réaction
///   5. 🧠  COMPLEXITÉ   : opérations réellement effectuées (théorie vs observé)
///
/// Export CSV : bouton en haut du panneau, touche E, ou automatiquement
/// à l'arrivée de l'Agent / à l'arrêt du Play.
///
/// Mise en place : GameObject vide « ComparisonHUD » → ce composant.
/// </summary>
public class ComparisonHUD : MonoBehaviour
{
    [Header("Références (vides = trouvées automatiquement)")]
    public GoalSeeker agent;          // l'IA A*
    public GuardFSM bandit;           // l'IA ad hoc
    public MCTSChaser chaser;         // l'IA Monte Carlo

    [Header("Affichage")]
    public bool showPanel = true;
    public KeyCode toggleKey = KeyCode.H;
    public KeyCode exportKey = KeyCode.E;
    public bool autoExportOnArrival = true;

    // ================================================================
    // 1) TEMPS
    // ================================================================
    [Header("⏱️ 1. Temps (lecture seule)")]
    public float timer;
    public float aStarTotalMs;
    public int aStarDecisions;
    public float mctsTotalMs;
    public int mctsDecisions;
    public float banditTotalMs;
    public int banditTicks;

    // ================================================================
    // 2) PERFORMANCE
    // ================================================================
    [Header("🎯 2. Performance (lecture seule)")]
    public bool agentArrived;              // réussite de l'Agent
    public float arrivalTime = -1f;
    public int contacts;                   // blocages réussis par le chasseur
    public int banditAttacks;              // attaques lancées par le bandit
    public float chaserWinRate;            // w/n moyen du MCTS

    // --- optimalité : distance réellement parcourue / distance directe ---
    public float agentTravel;
    public float banditTravel;
    public float chaserTravel;
    public float agentOptimality;          // 1,00 = ligne parfaite
    public float banditOptimality;
    public float chaserOptimality;

    // ================================================================
    // 3) MÉMOIRE
    // ================================================================
    [Header("💾 3. Mémoire (lecture seule)")]
    public int aStarNodes;                 // cases explorées (closedSet)
    public int mctsNodes;                  // nœuds de l'arbre
    public int adHocStates = 5;            // états de la FSM

    // ================================================================
    // 4) RÉACTIVITÉ
    // ================================================================
    [Header("🔄 4. Réactivité (lecture seule)")]
    public float aStarLatencyMs;           // ms entre deux recalculs
    public float mctsLatencyMs;            // ms entre deux décisions
    public float banditLatencyMs;          // ms entre deux images
    public float aStarDecisionsPerSec;
    public float mctsDecisionsPerSec;
    public float banditDecisionsPerSec;

    // ================================================================
    // 5) COMPLEXITÉ OBSERVÉE
    // ================================================================
    [Header("🧠 5. Complexité observée (lecture seule)")]
    public int aStarSearches;              // recherches A* effectuées
    public int aStarNodesTotal;            // nœuds explorés au total
    public int mctsSimulations;            // simulations jouées au total
    public int banditTransitions;          // transitions d'état de la FSM

    [Header("Export")]
    public string lastExportPath = "";

    bool cached;
    bool exportedOnArrival;
    float lastContactTime = -99f;
    string exportMessage;
    StringBuilder csv;

    // suivi des déplacements et des états
    Vector3 lastAgentPos, lastBanditPos, lastChaserPos;
    Vector3 startAgentPos, startBanditPos, startChaserPos;
    bool positionsReady;
    GuardFSM.State lastBanditState;

        GUIStyle styleTitle, styleLine, styleOk, styleHead;


    void Start()
    {
        csv = new StringBuilder();
        csv.AppendLine("temps_s;astar_ms;bandit_ms;mcts_ms;astar_noeuds;mcts_noeuds;contacts;" +
                       "agent_arrive;travel_A;travel_B;travel_C;optimalite_A;optimalite_B;optimalite_C;" +
                       "latence_A_ms;latence_B_ms;latence_C_ms");
    }

    void Update()
    {
        if (!cached) Cache();
        if (agent == null || chaser == null) return;

        timer += Time.deltaTime;

        TrackPositions();

        // ---------- 1) TEMPS ----------
        if (agent.pathfinding != null)
            aStarNodes = agent.pathfinding.closedSetVisual != null
                ? agent.pathfinding.closedSetVisual.Count : 0;

        if (agent.decisionCount != aStarDecisions)
        {
            aStarTotalMs += agent.lastDecisionMs;
            aStarDecisions = agent.decisionCount;
        }

        if (bandit != null && bandit.decisionCount != banditTicks)
        {
            banditTotalMs += bandit.lastDecisionMs;
            banditTicks = bandit.decisionCount;
        }

        mctsNodes = chaser.lastTreeNodes;
        if (chaser.decisionCount != mctsDecisions)
        {
            mctsTotalMs += chaser.lastDecisionMs;
            mctsDecisions = chaser.decisionCount;
        }

        // ---------- 2) PERFORMANCE ----------
        agentArrived = agent.HasArrived;
        if (agentArrived && arrivalTime < 0f)
        {
            arrivalTime = timer;
            if (autoExportOnArrival && !exportedOnArrival)
            {
                exportedOnArrival = true;
                ExportCsv();
            }
        }

        // contacts : même règle que GoalSeeker (4 s de cooldown)
        float d = Vector3.Distance(agent.transform.position, chaser.transform.position);
        if (d <= chaser.catchDistance && timer - lastContactTime >= 4f)
        {
            contacts++;
            lastContactTime = timer;
        }

        chaserWinRate = chaser.lastWinRate;

        // attaques et transitions du bandit (complexité observée de la FSM)
        if (bandit != null)
        {
            if (bandit.CurrentState != lastBanditState)
            {
                banditTransitions++;
                if (bandit.CurrentState == GuardFSM.State.Attack) banditAttacks++;
                lastBanditState = bandit.CurrentState;
            }
        }

        // ---------- 4) RÉACTIVITÉ (délai moyen entre deux décisions) ----------
        aStarLatencyMs = aStarDecisions > 0 ? (timer * 1000f) / aStarDecisions : 0f;
        mctsLatencyMs = mctsDecisions > 0 ? (timer * 1000f) / mctsDecisions : 0f;
        banditLatencyMs = banditTicks > 0 ? (timer * 1000f) / banditTicks : 0f;

        aStarDecisionsPerSec = timer > 0.1f ? aStarDecisions / timer : 0f;
        mctsDecisionsPerSec = timer > 0.1f ? mctsDecisions / timer : 0f;
        banditDecisionsPerSec = timer > 0.1f ? banditTicks / timer : 0f;

        // ---------- 5) COMPLEXITÉ OBSERVÉE ----------
        aStarSearches = agent.pathfinding != null ? agent.pathfinding.searchCount : 0;
        aStarNodesTotal = aStarSearches * Mathf.Max(1, aStarNodes);
        mctsSimulations = chaser.totalSimulations;

        // ---------- export ----------
        if (Input.GetKeyDown(exportKey)) ExportCsv();
        if (Input.GetKeyDown(toggleKey)) showPanel = !showPanel;

        // une ligne de CSV par seconde
        if (Mathf.Abs(timer % 1f) < Time.deltaTime)
        {
            csv.AppendLine($"{timer:F1};{AvgA():F3};{AvgBandit():F3};{AvgMcts():F2};" +
                           $"{aStarNodes};{mctsNodes};{contacts};{(agentArrived ? 1 : 0)};" +
                           $"{agentTravel:F1};{banditTravel:F1};{chaserTravel:F1};" +
                           $"{agentOptimality:F2};{banditOptimality:F2};{chaserOptimality:F2};" +
                           $"{aStarLatencyMs:F0};{banditLatencyMs:F0};{mctsLatencyMs:F0}");
        }
    }

    /// <summary>Distance parcourue et optimalité (trajet réel / distance directe).</summary>
    void TrackPositions()
    {
        Vector3 ap = agent.transform.position;
        Vector3 bp = bandit != null ? bandit.transform.position : Vector3.zero;
        Vector3 cp = chaser.transform.position;

        if (!positionsReady)
        {
            startAgentPos = ap;
            startBanditPos = bp;
            startChaserPos = cp;
            lastAgentPos = ap;
            lastBanditPos = bp;
            lastChaserPos = cp;
            if (bandit != null) lastBanditState = bandit.CurrentState;
            positionsReady = true;
            return;
        }

        agentTravel += Vector3.Distance(ap, lastAgentPos);
        if (bandit != null) banditTravel += Vector3.Distance(bp, lastBanditPos);
        chaserTravel += Vector3.Distance(cp, lastChaserPos);

        lastAgentPos = ap;
        lastBanditPos = bp;
        lastChaserPos = cp;

        // 1,00 = trajet parfaitement direct ; 2,00 = il a marché deux fois
        // la distance nécessaire (détours, hésitations).
        agentOptimality = Ratio(agentTravel, Vector3.Distance(startAgentPos, ap));
        if (bandit != null) banditOptimality = Ratio(banditTravel, Vector3.Distance(startBanditPos, bp));
        chaserOptimality = Ratio(chaserTravel, Vector3.Distance(startChaserPos, cp));
    }

    float Ratio(float travelled, float straight)
    {
        if (straight < 0.5f) return 0f;          // il n'a pas bougé : pas de trajet
        return travelled / straight;
    }

    void Cache()
    {
        if (agent == null) agent = FindFirstObjectByType<GoalSeeker>();
        if (bandit == null) bandit = FindFirstObjectByType<GuardFSM>();
        if (chaser == null) chaser = FindFirstObjectByType<MCTSChaser>();
        cached = agent != null && chaser != null;
    }

    // ---------------- moyennes ----------------
    public float AvgA() => aStarDecisions > 0 ? aStarTotalMs / aStarDecisions : 0f;
    public float AvgMcts() => mctsDecisions > 0 ? mctsTotalMs / mctsDecisions : 0f;
    public float AvgBandit() => banditTicks > 0 ? banditTotalMs / banditTicks : 0f;

    public float MemoryKo(int nodes, int bytesPerNode) => nodes * bytesPerNode / 1024f;

    // ---------------- panneau à l'écran ----------------
        void OnGUI()
    {
        if (styleTitle == null) BuildStyles();

        // ---- mise à l'échelle : le texte garde la même taille relative ----
        float scale = Screen.width / 1100f;
        if (scale <= 0f) scale = 1f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                                   new Vector3(scale, scale, 1f));

        if (!showPanel)
        {
            if (GUI.Button(new Rect(8, 8, 200, 44), "AFFICHER (H)")) showPanel = true;
            return;
        }

        float W = 1090f, H = 590f;

        // ---- fond OPAQUE : c'est ça qui supprime le flou ----
        GUI.color = new Color(0.07f, 0.09f, 0.12f, 0.96f);
        GUI.DrawTexture(new Rect(6, 6, W, H), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ---- bandeau de titre ----
        GUI.color = new Color(0.17f, 0.42f, 0.87f, 1f);
        GUI.DrawTexture(new Rect(6, 6, W, 56), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(20, 14, W - 40, 40),
                  "COMPARAISON DES 3 IA — les 3 tournent en même temps", styleHead);

        // ---- boutons ----
        float bw = (W - 60) / 2f;
        if (GUI.Button(new Rect(20, 72, bw, 46), "EXPORTER LE CSV")) ExportCsv();
        if (GUI.Button(new Rect(40 + bw, 72, bw, 46), "CACHER (H)")) showPanel = false;

        float cy = 128f;

        if (!string.IsNullOrEmpty(exportMessage))
        {
            GUI.Label(new Rect(20, cy, W - 40, 26), exportMessage, styleOk);
            cy += 28;
        }

        GUI.Label(new Rect(20, cy, W - 40, 26),
            $"durée {timer:F0} s      contacts MCTS : {contacts}      " +
            (agentArrived ? $"AGENT ARRIVÉ à {arrivalTime:F1} s" : "Agent bloqué"), styleTitle);
        cy += 34;

        float colW = (W - 60) / 2f;
        float x1 = 20f;
        float x2 = 20f + colW + 20f;
        float y1 = cy, y2 = cy;

        y1 = Section(x1, y1, colW, "1. TEMPS", new string[] {
            $"A*         {AvgA():F4} ms / décision",
            $"Ad hoc     {AvgBandit():F4} ms / décision",
            $"Monte Carlo {AvgMcts():F2} ms / décision",
            $"",
            $"total partie :  A* {aStarTotalMs:F1} ms   ·   MCTS {mctsTotalMs:F0} ms"
        });

        y1 = Section(x1, y1, colW, "3. MÉMOIRE", new string[] {
            $"A*          {aStarNodes} nœuds explorés   ≈ {MemoryKo(aStarNodes, 48):F1} Ko",
            $"Ad hoc      {adHocStates} états            ≈ 0,1 Ko",
            $"Monte Carlo {mctsNodes} nœuds d'arbre   ≈ {MemoryKo(mctsNodes, 96):F1} Ko"
        });

        y1 = Section(x1, y1, colW, "5. COMPLEXITÉ OBSERVÉE", new string[] {
            $"A*          {aStarSearches} recherches  ·  ~{aStarNodesTotal} nœuds",
            $"Ad hoc      {banditTransitions} transitions d'état",
            $"Monte Carlo {mctsSimulations} simulations jouées"
        });

        y2 = Section(x2, y2, colW, "2. PERFORMANCE", new string[] {
            agentArrived ? $"Agent       ARRIVÉ à {arrivalTime:F1} s" : "Agent       bloqué (n'arrive pas)",
            $"Bandit      {banditAttacks} attaques",
            $"Chasseur    {contacts} contact(s)  ·  victoire {chaserWinRate * 100f:F0} %",
            $"",
            $"OPTIMALITÉ (trajet / directe)",
            $"A* {agentOptimality:F2}     bandit {banditOptimality:F2}     chasseur {chaserOptimality:F2}"
        });

        y2 = Section(x2, y2, colW, "4. RÉACTIVITÉ", new string[] {
            $"délai entre deux décisions",
            $"A*          {aStarLatencyMs:F0} ms   ({aStarDecisionsPerSec:F1} déc/s)",
            $"Ad hoc      {banditLatencyMs:F0} ms   ({banditDecisionsPerSec:F1} déc/s)",
            $"Monte Carlo {mctsLatencyMs:F0} ms   ({mctsDecisionsPerSec:F1} déc/s)"
        });
    }

    /// <summary>Dessine un bloc titre + lignes et renvoie l'ordonnée suivante.</summary>
    float Section(float x, float y, float w, string title, string[] lines)
    {
        GUI.color = new Color(0.25f, 0.55f, 0.95f, 1f);
        GUI.DrawTexture(new Rect(x, y + 4, 5, 22 + lines.Length * 22), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x + 14, y, w - 14, 24), title, styleTitle);
        y += 28;

        foreach (string l in lines)
        {
            if (l.Length > 0) GUI.Label(new Rect(x + 14, y, w - 14, 22), l, styleLine);
            y += 22;
        }

        return y + 12;
    }

        void BuildStyles()
    {
        styleHead = new GUIStyle(GUI.skin.label);
        styleHead.fontSize = 22;
        styleHead.fontStyle = FontStyle.Bold;
        styleHead.alignment = TextAnchor.MiddleLeft;

        styleTitle = new GUIStyle(GUI.skin.label);
        styleTitle.fontSize = 18;
        styleTitle.fontStyle = FontStyle.Bold;
        styleTitle.alignment = TextAnchor.MiddleLeft;

        styleLine = new GUIStyle(GUI.skin.label);
        styleLine.fontSize = 16;
        styleLine.alignment = TextAnchor.MiddleLeft;

        styleOk = new GUIStyle(GUI.skin.label);
        styleOk.fontSize = 15;
        styleOk.fontStyle = FontStyle.Italic;
        styleOk.alignment = TextAnchor.MiddleLeft;
    }

    // ---------------- export ----------------
    void ExportCsv()
    {
        if (csv == null) return;

        string fileName = "comparaison_3_ia.csv";
        string content = csv.ToString();

        string p1 = Application.persistentDataPath + "/" + fileName;
        string p2 = Application.dataPath + "/" + fileName;
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        string p3 = home + "/Desktop/" + fileName;

        int ok = 0;
        ok += Write(p1, content) ? 1 : 0;
        ok += Write(p2, content) ? 1 : 0;
        ok += Write(p3, content) ? 1 : 0;

        exportMessage = ok > 0
            ? "CSV écrit (" + ok + " endroit(s)) — voir Console"
            : "Échec de l'écriture — voir Console";

        lastExportPath = ok > 0 ? p3 : "";

        Debug.Log("[MESURES] CSV -> " + p1);
        Debug.Log("[MESURES] CSV -> " + p2);
        Debug.Log("[MESURES] CSV -> " + p3 + (ok > 0 ? "  [OK]" : "  [ECHEC]"));

        if (ok == 0)
            Debug.LogError("[MESURES] Impossible d'écrire le CSV. Lines=" + csv.Length);
    }

    bool Write(string path, string content)
    {
        try
        {
            System.IO.File.WriteAllText(path, content);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MESURES] écriture impossible : " + path + " (" + e.Message + ")");
            return false;
        }
    }

    void OnDisable()
    {
        if (csv != null && csv.Length > 60) ExportCsv();
    }
}
