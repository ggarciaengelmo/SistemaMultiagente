using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class RoleAssignment
{
    public string ReceiverId;
    public string Role;
}

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

    // Parámetros de búsqueda con temporizador
    private bool searchPointSet = false;
    private Vector3 currentSearchPoint;
    private float searchTimer = 0f;
    private const float maxSearchTime = 15f;

    // Variables para asignación de roles
    private bool isCoordinator = false;
    private string currentCoordinatorId = null;
    private Dictionary<string, Vector3> proposalsReceived = new Dictionary<string, Vector3>();
    private bool assigningRoles = false;
    private string assignedRole = null;

    // Waypoints globales (configurables en inspector)
    [SerializeField] private Transform doorWaypoint;
    [SerializeField] private Transform treasureRoomWaypoint;
    [SerializeField] private Transform OxygenRoomWaypoint;
    [SerializeField] private Transform AdminRoomWaypoint;

    // Waypoints de intercepción para cada zona
    [SerializeField] private Transform interceptEast1;
    [SerializeField] private Transform interceptEast2;
    [SerializeField] private Transform interceptWest1;
    [SerializeField] private Transform interceptWest2;
    [SerializeField] private Transform interceptNorth1;
    [SerializeField] private Transform interceptNorth2;

    // Waypoints asignados dinámicamente en subasta
    private Transform interceptWaypoint1 = null;
    private Transform interceptWaypoint2 = null;

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

        // Suscripción a canal de comunicación
        CommunicationChannel.Instance.OnMessagePublished += OnMessageReceived;
    }

    void Update()
    {
        UpdateBeliefs();
        GenerateDesires();
        SelectIntention();
        ExecuteIntention();
    }

    // ——— Métodos BDI ———
    void UpdateBeliefs() { /* actualización desde sensores */ }
    void GenerateDesires()
    {
        desires.Clear();
        if ((bool)worldState["isThiefSeen"]) desires.Add("CatchThief");
        else if ((bool)worldState["isThiefHeard"]) desires.Add("InvestigateNoise");
        if ((bool)worldState["isTreasureStolen"]) desires.Add("ProtectDoor");
        desires.Add("Patrol");
    }
    void SelectIntention()
    {
        if (assignedRole == "cortar_camino_1" || assignedRole == "cortar_camino_2")
            currentIntention = "Intercept";
        else if (assignedRole == "vigilar")
            currentIntention = ((bool)worldState["isTreasureStolen"]) ? "ProtectDoor" : "ProtectTreasure";
        else if (isCoordinator && (bool)worldState["isThiefSeen"])
            currentIntention = "CatchThief";
        else if (desires.Contains("CatchThief"))
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
            case "CatchThief": PursueThief(); break;
            case "InvestigateNoise": AlertState(); break;
            case "ProtectDoor": StayAtDoor(); break;
            case "ProtectTreasure": GoToTreasureRoom(); break;
            case "Patrol": Patrol(); break;
            case "Intercept": InterceptThief(); break;
        }
    }

    // ——— Actualización de estado y sensores ———
    public void UpdateState(bool? isThiefHeard = null, bool? isThiefSeen = null,
        Vector3? thiefPosition = null, Vector3? noisePosition = null,
        bool? isTreasureStolen = null, bool? WithTreasure = null)
    {
        if (isThiefHeard.HasValue)
        {
            worldState["isThiefHeard"] = isThiefHeard.Value;
            searchTimer = 0f; searchPointSet = false;
        }
        if (isThiefSeen.HasValue) worldState["isThiefSeen"] = isThiefSeen.Value;
        if (thiefPosition.HasValue) worldState["thiefPosition"] = thiefPosition.Value;
        if (noisePosition.HasValue) worldState["noisePosition"] = noisePosition.Value;
        if (isTreasureStolen.HasValue) worldState["isTreasureStolen"] = isTreasureStolen.Value;
        if (WithTreasure.HasValue) worldState["WithTreasure"] = WithTreasure.Value;
    }

    public void OnNoiseDetected(Vector3 zona) =>
        UpdateState(isThiefHeard: true, noisePosition: zona);

    public void SomeoneSeen(bool detected, Vector3 pos)
    {
        UpdateState(isThiefSeen: detected, thiefPosition: pos, isTreasureStolen: GlobalGameState.TreasureStolen);
        if (!detected) return;

        if (!isCoordinator && currentCoordinatorId == null)
        {
            isCoordinator = true;
            currentCoordinatorId = gameObject.name;
            Debug.Log($"{gameObject.name} se autoproclama COORDINADOR.");
            StartCoroutine(StartRoleAssignment());
        }
        else if (!isCoordinator && assignedRole != null)
        {
            Debug.Log($"{gameObject.name} abandona el rol '{assignedRole}' para atrapar al ladrón.");
            assignedRole = null;
        }
        // Informar a otros
        var msg = new Message(gameObject.name, "thief-spotted", Performative.Inform, pos);
        CommunicationChannel.Instance.Publish(msg);
    }

    // ——— Actuaciones básicas ———
    void AlertState() // Escuchado pero no ha sido visto
    {
        // L�gica para buscar al ladr�n en el �rea, por ejemplo, patrullando �reas cercanas
        // Si a�n no se ha definido un punto de b�squeda, se genera uno aleatorio
        if (!searchPointSet)
        {
            float searchRadius = 50f; // Define el radio de b�squeda alrededor de la �ltima posici�n conocida
            Vector3 noisePosition = (Vector3)worldState["noisePosition"];
            currentSearchPoint = noisePosition + new Vector3(Random.Range(-searchRadius, searchRadius), 0, Random.Range(-searchRadius, searchRadius));
            searchPointSet = true;
            actuator.MoveToTarget(currentSearchPoint);
            Debug.Log("Alerta: buscan en punto aleatorio: " + currentSearchPoint);
        }
        else
        {
            // Si el polic�a ya alcanz� el punto o se para porque no puede alcanzarlo, se genera uno nuevo
            var navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();

            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f || (navMeshAgent != null && navMeshAgent.velocity.magnitude == 0.0f))
            {
                searchPointSet = false;
            }
        }
    }

    void PursueThief()
    {
        // Mueve al polic�a hacia el ladr�n
        Vector3 thiefPosition = (Vector3)worldState["thiefPosition"];
        actuator.MoveToTarget(thiefPosition);

    }

    void Patrol()
    {
        // L�gica de caminar mientras patrulla
        actuator.Walking();
    }

    void GoToTreasureRoom()
    {
        actuator.MoveToTarget(treasureRoomWaypoint.position);

        // Si el policía ya llegó (o está muy cerca) del tesoro, sabe si ha sido robado o no
        if (Vector3.Distance(transform.position, treasureRoomWaypoint.position) < 10f)
        {
            UpdateState(WithTreasure: true, isTreasureStolen: GlobalGameState.TreasureStolen);
        }
    }

    /// <summary>
    /// Mueve al policía a la puerta y lo deja allí quieto
    /// </summary>
    void StayAtDoor()
    {
        actuator.MoveToTarget(doorWaypoint.position);
    }
    void InterceptThief()
    {
        if (assignedRole == "cortar_camino_1" && interceptWaypoint1 != null)
            actuator.MoveToTarget(interceptWaypoint1.position);
        else if (assignedRole == "cortar_camino_2" && interceptWaypoint2 != null)
            actuator.MoveToTarget(interceptWaypoint2.position);
        else
            Debug.LogWarning($"{gameObject.name} no tiene waypoint de intercepción válido.");
    }

    // ——— Comunicación y subasta de roles ———
    private void OnMessageReceived(Message msg)
    {
        if (msg.SenderId == gameObject.name) return;
        if (msg.ConversationId == "assign-role")
        {
            if (msg.Performative == Performative.Cfp && !isCoordinator)
                ProposeRole();
            else if (msg.Performative == Performative.Propose && isCoordinator)
                ReceiveProposal(msg);
            else if (msg.Performative == Performative.Accept)
                ReceiveAssignment(msg);
        }
        else if (msg.Performative == Performative.Inform && msg.ConversationId == "thief-spotted")
        {
            if (msg.Content is Vector3 p) UpdateState(isThiefSeen: true, thiefPosition: p);
        }
    }

    private void ProposeRole()
    {
        var proposal = new Message(gameObject.name, "assign-role", Performative.Propose, transform.position);
        CommunicationChannel.Instance.Publish(proposal);
        Debug.Log($"{gameObject.name} propuso posición.");
    }

    private void ReceiveProposal(Message msg)
    {
        if (msg.Content is Vector3 pos)
        {
            proposalsReceived[msg.SenderId] = pos;
            if (!assigningRoles)
            {
                assigningRoles = true;
                StartCoroutine(AssignRolesAfterDelay());
            }
        }
    }

    private void ReceiveAssignment(Message msg)
    {
        if (msg.Content is RoleAssignment ra && ra.ReceiverId == gameObject.name)
        {
            assignedRole = ra.Role;
            Debug.Log($"{gameObject.name} recibe rol '{assignedRole}'.");
        }
    }

    IEnumerator StartRoleAssignment()
    {
        yield return new WaitForSeconds(0.5f);
        var cfp = new Message(gameObject.name, "assign-role", Performative.Cfp, "¿Colaboran?");
        CommunicationChannel.Instance.Publish(cfp);
        Debug.Log($"{gameObject.name} lanza CFP.");
    }

    IEnumerator AssignRolesAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        if (proposalsReceived.Count == 0) { assigningRoles = false; yield break; }

        // Elegir zona y waypoints
        string zone = CalculateThiefRelativePosition();
        Transform b1 = null, b2 = null;
        switch (zone)
        {
            case "East": b1 = interceptEast1; b2 = interceptEast2; break;
            case "West": b1 = interceptWest1; b2 = interceptWest2; break;
            case "North": b1 = interceptNorth1; b2 = interceptNorth2; break;
            case "South":
                if ((bool)worldState["isTreasureStolen"]) { b1 = doorWaypoint; b2 = doorWaypoint; }
                else { b1 = treasureRoomWaypoint; b2 = doorWaypoint; }
                break;
        }
        interceptWaypoint1 = b1; interceptWaypoint2 = b2;

        var roles = DetermineRoleAssignmentsByProximity(b1, b2);
        foreach (var r in roles) SendAccept(r.id, r.role);

        proposalsReceived.Clear(); assigningRoles = false;
    }

    private List<(string id, float distance)> QuienEstaCerca(Vector3 wp, Dictionary<string, Vector3> cand) =>
        cand.Select(e => (e.Key, Vector3.Distance(e.Value, wp)))
            .OrderBy(t => t.Item2).Select(t => (t.Key, t.Item2)).ToList();

    private List<(string id, string role)> DetermineRoleAssignmentsByProximity(Transform p1, Transform p2)
    {
        var list = new List<(string, string)>();
        var remaining = new Dictionary<string, Vector3>(proposalsReceived);
        const string R1 = "cortar_camino_1", R2 = "cortar_camino_2", R3 = "vigilar";
        if (p1 != null && remaining.Any())
        {
            var near1 = QuienEstaCerca(p1.position, remaining)[0];
            list.Add((near1.id, R1)); remaining.Remove(near1.id);
        }
        if (p2 != null && remaining.Any())
        {
            var near2 = QuienEstaCerca(p2.position, remaining)[0];
            list.Add((near2.id, R2)); remaining.Remove(near2.id);
        }
        if (remaining.Any()) list.Add((remaining.Keys.First(), R3));
        return list;
    }

    private string CalculateThiefRelativePosition()
    {
        if (worldState.TryGetValue("thiefPosition", out var obj) && obj is Vector3 tp)
        {
            if (tp.x > -77f) return "East";
            if (tp.x < -600f || (Vector3.Distance(tp, OxygenRoomWaypoint.position) < 100f
                && Vector3.Distance(tp, AdminRoomWaypoint.position) > 110f)) return "West";
            if (tp.z < 20f) return "North";
            return "South";
        }
        return string.Empty;
    }

    void SendAccept(string id, string role)
    {
        var ra = new RoleAssignment { ReceiverId = id, Role = role };
        var msg = new Message(gameObject.name, "assign-role", Performative.Accept, ra);
        CommunicationChannel.Instance.Publish(msg);
        Debug.Log($"{gameObject.name} asigna {role} a {id}.");
    }
}
