using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Asegúrate de tener definidas también estas clases/enums en tu proyecto:
// public enum Performative { Cfp, Propose, Accept, Reject, Inform /*…*/ }
// [Serializable] public class Message { public string SenderId; public string ConversationId; public Performative Performative; public object Content; /* constructor… */ }
// public class CommunicationChannel : MonoBehaviour { public static CommunicationChannel Instance; public event Action<Message> OnMessagePublished; public void Publish(Message msg) { /*…*/ } }

public static class GlobalGameState
{
    public static bool TreasureStolen = false;
}

public class policeBrain : MonoBehaviour
{
    // Sensores y actuadores
    private policeSensor sensor;
    private policeActuator actuator;

    // Creencias (beliefs)
    private Dictionary<string, object> worldState;

    // Deseos (desires) e Intención actual
    private List<string> desires = new List<string>();
    private string currentIntention = "";

    // Parámetros de búsqueda BDI
    private bool searchPointSet = false;
    private Vector3 currentSearchPoint;
    private float searchTimer = 0f;
    private const float maxSearchTime = 15f;

    [SerializeField] private Transform doorWaypoint;
    [SerializeField] private Transform treasureRoomWaypoint;

    // —— Campos para la subasta única ——
    private bool amICoordinator = false;
    private bool auctionSent = false;
    private string auctionConversationId;
    private float auctionTimer = 0f;
    private const float auctionTimeout = 3f;   // segundos para recibir propuestas
    private Dictionary<string, float> proposals = new Dictionary<string, float>();

    void Awake()
    {
        // Inicializamos creencias
        worldState = new Dictionary<string, object>
        {
            {"isThiefHeard", false},
            {"isThiefSeen", false},
            {"isTreasureStolen", false},
            {"thiefPosition", Vector3.zero},
            {"noisePosition", Vector3.zero},
            {"WithTreasure", false}
        };

        sensor = GetComponent<policeSensor>();
        actuator = GetComponent<policeActuator>();

        // Suscribirse al canal de mensajería
        CommunicationChannel.Instance.OnMessagePublished += OnMessageReceived;
    }

    void Update()
    {
        // ─── Gestión de subasta en background ───
        if (amICoordinator)
        {
            // 1️⃣ Enviar CFP una sola vez
            if (!auctionSent)
            {
                auctionSent = true;
                auctionConversationId = Guid.NewGuid().ToString();
                var thiefPos = (Vector3)worldState["thiefPosition"];
                string content = "ThiefPos:" + thiefPos.ToString();
                var cfp = new Message(
                    senderId:       gameObject.name,
                    conversationId: auctionConversationId,
                    performative:   Performative.Cfp,
                    content:        content
                );
                CommunicationChannel.Instance.Publish(cfp);
                Debug.Log($"[Auction] CFP sent by {gameObject.name}, convoId={auctionConversationId}, content={content}");
            }

            // 2️⃣ Contar tiempo y cerrar subasta
            auctionTimer += Time.deltaTime;
            if (auctionTimer >= auctionTimeout)
            {
                auctionTimer = 0f;
                amICoordinator = false;   // Ya no coordino
                ProcessAuction();
            }
        }

        // ─── Ciclo BDI normal ───
        UpdateBeliefs();
        GenerateDesires();
        SelectIntention();
        ExecuteIntention();
    }

    // ——— Métodos BDI ———

    void UpdateBeliefs()
    {
        // Las creencias se actualizan vía UpdateState / sensores
    }

    void GenerateDesires()
    {
        desires.Clear();

        if ((bool)worldState["isThiefSeen"])
            desires.Add("CatchThief");
        else if ((bool)worldState["isThiefHeard"])
            desires.Add("InvestigateNoise");

        if ((bool)worldState["isTreasureStolen"])
            desires.Add("ProtectDoor");

        desires.Add("Patrol");
    }

    void SelectIntention()
    {
        if (desires.Contains("CatchThief"))
            currentIntention = "CatchThief";
        else if (desires.Contains("InvestigateNoise"))
            currentIntention = "InvestigateNoise";
        else if (desires.Contains("ProtectDoor"))
            currentIntention = "ProtectDoor";
        else
            currentIntention = "Patrol";
    }

    void ExecuteIntention()
    {
        switch (currentIntention)
        {
            case "CatchThief":      PursueThief();      break;
            case "InvestigateNoise": AlertState();      break;
            case "ProtectDoor":      StayAtDoor();      break;
            case "Patrol":           Patrol();          break;
        }
    }

    // ——— Gestión de creencias ———

    public void UpdateState(
        bool? isThiefHeard = null,
        bool? isThiefSeen = null,
        Vector3? thiefPosition = null,
        Vector3? noisePosition = null,
        bool? isTreasureStolen = null,
        bool? WithTreasure = null)
    {
        if (isThiefHeard.HasValue)     worldState["isThiefHeard"]     = isThiefHeard.Value;
        if (isThiefSeen.HasValue)      worldState["isThiefSeen"]      = isThiefSeen.Value;
        if (thiefPosition.HasValue)    worldState["thiefPosition"]    = thiefPosition.Value;
        if (noisePosition.HasValue)    worldState["noisePosition"]    = noisePosition.Value;
        if (isTreasureStolen.HasValue) worldState["isTreasureStolen"] = isTreasureStolen.Value;
        if (WithTreasure.HasValue)     worldState["WithTreasure"]     = WithTreasure.Value;
    }

    public void OnNoiseDetected(Vector3 zona)
    {
        UpdateState(isThiefHeard: true, noisePosition: zona);
    }

    public void SomeoneSeen(bool detected, Vector3 pos)
    {
        UpdateState(isThiefSeen: detected, thiefPosition: pos, isTreasureStolen: GlobalGameState.TreasureStolen);

        if (detected && !amICoordinator)
        {
            // Marco que voy a coordinar la próxima subasta
            amICoordinator = true;
            auctionSent = false;
            auctionTimer = 0f;
            proposals.Clear();
        }
    }

    // ——— Mensajería y subasta ———

    void OnMessageReceived(Message msg)
    {
        if (msg.SenderId == gameObject.name) return;

        // —— Coordinador: recoger propuestas ——
        if (amICoordinator
            && msg.Performative == Performative.Propose
            && msg.ConversationId == auctionConversationId)
        {
            string contentStr = msg.Content.ToString();  // "Distance:12.34"
            var parts = contentStr.Split(':');
            if (parts.Length == 2)
            {
                string numberStr = parts[1].Trim();
                if (float.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float dist))
                {
                    proposals[msg.SenderId] = dist;
                    Debug.Log($"[Auction] Proposal received from {msg.SenderId}: dist={dist}");
                }
                else
                {
                    Debug.LogWarning($"[Auction] No pude parsear distancia «{numberStr}» de {msg.SenderId}");
                }
            }
            else
            {
                Debug.LogWarning($"[Auction] Formato inesperado en Content: «{contentStr}»");
            }
        }

        // —— Supporters: responder al CFP ——
        if (!amICoordinator
            && msg.Performative == Performative.Cfp)
        {
            // Extraer posición del ladrón
            string content = msg.Content.ToString();             // "ThiefPos:(x,y,z)"
            string posStr = content.Split(new[] {':'}, 2)[1].Trim(); // "(x,y,z)"
            Vector3 thiefPos = ParseVector3(posStr);

            // Calcular distancia
            float dist = Vector3.Distance(transform.position, thiefPos);

            // Enviar propuesta
            string proposalContent = "Distance:" + dist.ToString("F2", CultureInfo.InvariantCulture);
            var propose = new Message(
                senderId:       gameObject.name,
                conversationId: msg.ConversationId,
                performative:   Performative.Propose,
                content:        proposalContent
            );
            CommunicationChannel.Instance.Publish(propose);
            Debug.Log($"[Auction] {gameObject.name} sent PROPOSE: dist={proposalContent}");
        }
    }

    // Helper para convertir "(x,y,z)" en Vector3
    Vector3 ParseVector3(string s)
    {
        s = s.Replace("(", "").Replace(")", "");
        var parts = s.Split(',');
        return new Vector3(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture)
        );
    }

    void ProcessAuction()
    {
        Debug.Log($"[Auction] Closing auction {auctionConversationId}. {proposals.Count} proposals.");

        // Ordenar participantes por distancia ascendente
        var sorted = new List<KeyValuePair<string, float>>(proposals);
        sorted.Sort((a, b) => a.Value.CompareTo(b.Value));

        // Elegir dos Blockers y asignar monitores al resto
        string blockerA = sorted.Count > 0 ? sorted[0].Key : null;
        string blockerB = sorted.Count > 1 ? sorted[1].Key : null;
        List<string> monitors = new List<string>();
        for (int i = 2; i < sorted.Count; i++)
            monitors.Add(sorted[i].Key);

        Debug.Log($"[Auction] Blockers: {blockerA}, {blockerB}. Monitors: {string.Join(", ", monitors)}");

        // Aquí podrías enviar ACCEPT/REJECT o asignar flags de rol:
        // if (gameObject.name == blockerA || gameObject.name == blockerB) assignedRole = "Blocker";
        // else if (monitors.Contains(gameObject.name)) assignedRole = "Monitor";
    }

    // ——— Acciones de agente ———

    void Patrol()
    {
        actuator.Walking();
    }

    void AlertState()
    {
        if (!searchPointSet)
        {
            float radius = 50f;
            Vector3 pos = (Vector3)worldState["noisePosition"];
            currentSearchPoint = pos + new Vector3(UnityEngine.Random.Range(-radius, radius), 0, UnityEngine.Random.Range(-radius, radius));
            searchPointSet = true;
            actuator.MoveToTarget(currentSearchPoint);
        }
        else
        {
            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f
                || (nav != null && nav.velocity.magnitude == 0f))
                searchPointSet = false;
        }
    }

    void PursueThief()
    {
        Vector3 pos = (Vector3)worldState["thiefPosition"];
        actuator.MoveToTarget(pos);
    }

    void StayAtDoor()
    {
        actuator.MoveToTarget(doorWaypoint.position);
    }
}
