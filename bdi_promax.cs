using System.Collections;
using System.Collections.Generic;
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

    private bool isCoordinator = false;
    private string currentCoordinatorId = null;
    private Dictionary<string, Vector3> proposalsReceived = new Dictionary<string, Vector3>();
    private bool assigningRoles = false;
    private string assignedRole = null;

    [SerializeField] private Transform interceptWaypoint1;
    [SerializeField] private Transform interceptWaypoint2;
    [SerializeField] private Transform doorWaypoint;
    [SerializeField] private Transform treasureRoomWaypoint;

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

        CommunicationChannel.Instance.OnMessagePublished += OnMessageReceived;

    }

    void Update()
    {
        // 1️⃣ Actualizar beliefs
        UpdateBeliefs();
        // 2️⃣ Generar deseos
        GenerateDesires();
        // 3️⃣ Seleccionar intención
        SelectIntention();
        // 4️⃣ Ejecutar intención
        ExecuteIntention();
    }

    // ——— Métodos BDI ———

    void UpdateBeliefs()
    {
        // Creencias actualizadas por UpdateState / sensores
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
        // Intenciones forzadas por rol asignado
        if (assignedRole == "cortar_camino_1" || assignedRole == "cortar_camino_2")
        {
            currentIntention = "Intercept";
        }
        else if (assignedRole == "vigilar")
        {
            currentIntention = "ProtectDoor"; // Todo Proteger puerta o tesoro depensiendo de si hay tesoro
        }
        // El coordinador, que vio al ladrón, persigue
        else if (isCoordinator && (bool)worldState["isThiefSeen"])
        {
            currentIntention = "CatchThief";
        }
        // Si no hay rol asignado, usar deseos normales
        else if (desires.Contains("CatchThief"))
        {
            currentIntention = "CatchThief";
        }
        else if (desires.Contains("InvestigateNoise"))
        {
            currentIntention = "InvestigateNoise";
        }
        else if (desires.Contains("ProtectDoor"))
        {
            currentIntention = "ProtectDoor";
        }
        else
        {
            currentIntention = "Patrol";
        }
    }



    void ExecuteIntention()
    {
        switch (currentIntention)
        {
            case "CatchThief":
                PursueThief();
                break;
            case "InvestigateNoise":
                AlertState();
                break;
            case "ProtectDoor":
                StayAtDoor();
                break;
            case "Patrol":
                Patrol();
                break;
            case "Intercept":
                InterceptThief();
                break;

        }
    }

    // ——— Lógica de actuación con temporizador ———

    public void UpdateState(
        bool? isThiefHeard = null,
        bool? isThiefSeen = null,
        Vector3? thiefPosition = null,
        Vector3? noisePosition = null,
        bool? isTreasureStolen = null,
        bool? WithTreasure = null)
    {
        if (isThiefHeard.HasValue)
        {
            worldState["isThiefHeard"] = isThiefHeard.Value;
            // Al cambiar estado de oído, reiniciar temporizador y búsqueda
            searchTimer = 0f;
            searchPointSet = false;
        }
        if (isThiefSeen.HasValue) worldState["isThiefSeen"] = isThiefSeen.Value;
        if (thiefPosition.HasValue) worldState["thiefPosition"] = thiefPosition.Value;
        if (noisePosition.HasValue) worldState["noisePosition"] = noisePosition.Value;
        if (isTreasureStolen.HasValue) worldState["isTreasureStolen"] = isTreasureStolen.Value;
        if (WithTreasure.HasValue) worldState["WithTreasure"] = WithTreasure.Value;
    }

    public void OnNoiseDetected(Vector3 zonaAproximada)
    {
        UpdateState(isThiefHeard: true, noisePosition: zonaAproximada);

    }


    public void SomeoneSeen(bool detected, Vector3 detectedPosition)
    {
        UpdateState(isThiefSeen: detected, thiefPosition: detectedPosition, isTreasureStolen: GlobalGameState.TreasureStolen);

        if (detected)
        {
            // Solo el primero que detecta se convierte en coordinador
            if (!isCoordinator && currentCoordinatorId == null)
            {
                isCoordinator = true;
                currentCoordinatorId = gameObject.name;
                Debug.Log($"{gameObject.name} se autoproclama COORDINADOR.");

                StartCoroutine(StartRoleAssignment());
            }

            // Si ya tengo un rol asignado pero veo al ladrón, lo abandono
            if (!isCoordinator && assignedRole != null)
            {
                Debug.Log($"{gameObject.name} abandona el rol '{assignedRole}' para atrapar al ladrón.");
                assignedRole = null;
            }

            // Enviar mensaje para compartir posición del ladrón
            Message msg = new Message(
                senderId: gameObject.name,
                conversationId: "thief-spotted",
                performative: Performative.Inform,
                content: detectedPosition
            );
            CommunicationChannel.Instance.Publish(msg);
        }
    }




    void Patrol()
    {
        actuator.Walking();
    }

    void AlertState()
    {
        // Incrementamos tiempo de búsqueda
        searchTimer += Time.deltaTime;
        if (searchTimer >= maxSearchTime)
        {
            // Tiempo agotado: dejar de buscar y volver a patrullar
            UpdateState(isThiefHeard: false);
            return;
        }

        if (!searchPointSet)
        {
            float radius = 50f;
            Vector3 pos = (Vector3)worldState["noisePosition"];
            currentSearchPoint = pos + new Vector3(Random.Range(-radius, radius), 0, Random.Range(-radius, radius));
            searchPointSet = true;
            actuator.MoveToTarget(currentSearchPoint);
        }
        else
        {
            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f ||
                (nav != null && nav.velocity.magnitude == 0f))
            {
                // Generar nuevo punto de búsqueda
                searchPointSet = false;
            }
        }
    }

    void PursueThief()
    {
        Vector3 pos = (Vector3)worldState["thiefPosition"];
        actuator.MoveToTarget(pos);
    }

    void GoToTreasureRoom()
    {
        actuator.MoveToTarget(treasureRoomWaypoint.position);
        if (Vector3.Distance(transform.position, treasureRoomWaypoint.position) < 10f)
        {
            UpdateState(WithTreasure: true, isTreasureStolen: GlobalGameState.TreasureStolen);
        }
    }

    void StayAtDoor()
    {
        actuator.MoveToTarget(doorWaypoint.position);
    }

    void InterceptThief()
    {
        if (assignedRole == "cortar_camino_1" && interceptWaypoint1 != null)
        {
            actuator.MoveToTarget(interceptWaypoint1.position);
        }
        else if (assignedRole == "cortar_camino_2" && interceptWaypoint2 != null)
        {
            actuator.MoveToTarget(interceptWaypoint2.position);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} no tiene asignado un waypoint de intercepción válido.");
        }
    }



    private void OnMessageReceived(Message msg)
    {
        // Ignorar si el mensaje fue enviado por uno mismo
        if (msg.SenderId == gameObject.name)
            return;

        // Si es un mensaje de avistamiento del ladrón
        if (msg.Performative == Performative.Inform && msg.ConversationId == "thief-spotted")
        {
            if (msg.Content is Vector3 thiefPos)
            {
                Debug.Log($"{gameObject.name} recibió info de ladrón en {thiefPos}");
                UpdateState(isThiefSeen: true, thiefPosition: thiefPos);
            }
        }
        else if (msg.Performative == Performative.Cfp && msg.ConversationId == "assign-role")
        {
            if (!isCoordinator)
            {
                Vector3 myPosition = transform.position;

                Message proposal = new Message(
                    senderId: gameObject.name,
                    conversationId: "assign-role",
                    performative: Performative.Propose,
                    content: myPosition
                );

                CommunicationChannel.Instance.Publish(proposal);
                Debug.Log($"{gameObject.name} respondió al CFP con su posición.");
            }
        }
        else if (msg.Performative == Performative.Propose && msg.ConversationId == "assign-role" && isCoordinator)
        {
            if (msg.Content is Vector3 position)
            {
                proposalsReceived[msg.SenderId] = position;
                Debug.Log($"{gameObject.name} registró propuesta de {msg.SenderId}");

                // Si es la primera propuesta, lanza el temporizador de asignación
                if (!assigningRoles)
                {
                    assigningRoles = true;
                    StartCoroutine(AssignRolesAfterDelay());
                }
            }
        }
        else if (msg.Performative == Performative.Accept && msg.ConversationId == "assign-role")
        {
            if (msg.Content is RoleAssignment assignment && assignment.ReceiverId == gameObject.name)
            {
                assignedRole = assignment.Role;
                Debug.Log($"{gameObject.name} recibió el rol '{assignment.Role}' y se prepara para ejecutarlo.");
            }
        }


    }
    /// <summary>
    /// Calcula la distancia desde la posición de cada policía que envió una propuesta
    /// hasta un waypoint específico y devuelve una lista ordenada por distancia.
    /// </summary>
    /// <param name="waypoint">El punto de referencia para calcular las distancias.</param>
    /// <param name="candidates">El diccionario de candidatos (id -> posición) a considerar.</param>
    /// <returns>Una lista de tuplas (id del policía, distancia al waypoint) ordenada ascendentemente por distancia.</returns>
    private List<(string id, float distance)> QuienEstaCerca(Vector3 waypoint, Dictionary<string, Vector3> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return new List<(string id, float distance)>(); // Devuelve lista vacía si no hay candidatos
        }

        return candidates
            .Select(entry => (id: entry.Key, distance: Vector3.Distance(entry.Value, waypoint)))
            .OrderBy(item => item.distance)
            .ToList();
    }

    /// <summary>
    /// Devuelve los costes calculados y ordenados
    /// </summary>
    /// <param name="thiefPosition"></param>
    /// <returns></returns>
    private List<(string id, float distance)> CalculateAndSortCosts(Vector3 thiefPosition)
    {
        // Calculo distancia al ladrón
        return QuienEstaCerca(thiefPosition);

        // Calculo según waypoints

    }
    private List<(string id, string role)> DetermineRoleAssignmentsByProximity(Transform interceptionPoint1, Transform interceptionPoint2)
    {
        var assignments = new List<(string id, string role)>();
        // Copiamos el diccionario para poder modificarlo sin afectar el original durante el cálculo
        var remainingCandidates = new Dictionary<string, Vector3>(proposalsReceived);

        // Roles a asignar
        const string ROLE_INTERCEPT_1 = "cortar_camino_1";
        const string ROLE_INTERCEPT_2 = "cortar_camino_2";
        const string ROLE_GUARD = "vigilar";

        // --- Asignación 1: Más cercano a interceptionPoint1 ---
        if (interceptionPoint1 != null && remainingCandidates.Count > 0)
        {
            List<(string id, float distance)> sortedByWp1 = QuienEstaCerca(interceptionPoint1.position, remainingCandidates);
            if (sortedByWp1.Count > 0)
            {
                string assignedId = sortedByWp1[0].id;
                assignments.Add((assignedId, ROLE_INTERCEPT_1));
                remainingCandidates.Remove(assignedId); // Quitar al asignado de los candidatos
                Debug.Log($"Asignación Preliminar 1: {assignedId} -> {ROLE_INTERCEPT_1}");
            }
        }
        else if (interceptionPoint1 == null)
        {
             Debug.LogWarning($"{gameObject.name}: El primer waypoint de intercepción no es válido. No se puede asignar {ROLE_INTERCEPT_1}.");
        }

        // --- Asignación 2: Más cercano (restante) a interceptionPoint2 ---
        if (interceptionPoint2 != null && remainingCandidates.Count > 0)
        {
            List<(string id, float distance)> sortedByWp2 = QuienEstaCerca(interceptionPoint2.position, remainingCandidates);
             if (sortedByWp2.Count > 0)
            {
                string assignedId = sortedByWp2[0].id;
                assignments.Add((assignedId, ROLE_INTERCEPT_2));
                remainingCandidates.Remove(assignedId); // Quitar al asignado de los candidatos
                Debug.Log($"Asignación Preliminar 2: {assignedId} -> {ROLE_INTERCEPT_2}");
            }
        }
         else if (interceptionPoint2 == null)
        {
             Debug.LogWarning($"{gameObject.name}: El segundo waypoint de intercepción no es válido. No se puede asignar {ROLE_INTERCEPT_2}.");
        }

        // --- Asignación 3: Un restante para vigilar ---
        // Simplemente tomamos el primero que quede en la lista de restantes.
        if (remainingCandidates.Count > 0)
        {
            // Tomamos el primer ID que quede en el diccionario de restantes
            string assignedId = remainingCandidates.Keys.First(); // Asumiendo que usas LINQ aquí, si no, usa el bucle foreach
            assignments.Add((assignedId, ROLE_GUARD));
            remainingCandidates.Remove(assignedId);
            Debug.Log($"Asignación Preliminar 3: {assignedId} -> {ROLE_GUARD}");
        }

        return assignments;
    }

    IEnumerator AssignRolesAfterDelay()
    {
        yield return new WaitForSeconds(2f); // Espera para recibir propuestas

        // Asegurarse de que tenemos propuestas antes de asignar
        if (proposalsReceived.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: No se recibieron propuestas. No se asignarán roles.");
            assigningRoles = false;
            yield break;
        }

         // 1. Determinar las asignaciones basadas en proximidad a waypoints
        List<(string id, string role)> roleAssignments = DetermineRoleAssignmentsByProximity(interceptWaypoint1, interceptWaypoint2);

        // 2. Enviar los mensajes de aceptación para cada asignación
        foreach (var assignment in roleAssignments)
        {
            SendAccept(assignment.id, assignment.role);
        }

        // 3. Limpiar estado
        proposalsReceived.Clear();
        assigningRoles = false;
        Debug.Log($"{gameObject.name} ha terminado de asignar roles basados en waypoints.");
    }

    IEnumerator StartRoleAssignment()
    {
        yield return new WaitForSeconds(0.5f); // pequeña pausa para asegurar que otros reciben el CFP

        // Enviar CFP a todos los demás
        Message cfp = new Message(
            senderId: gameObject.name,
            conversationId: "assign-role",
            performative: Performative.Cfp,
            content: "¿Quién puede colaborar?"
        );
        CommunicationChannel.Instance.Publish(cfp);

        Debug.Log($"{gameObject.name} lanzó CFP para asignar roles.");
    }

    void SendAccept(string receiverId, string role)
    {
        var content = new RoleAssignment
        {
            ReceiverId = receiverId,
            Role = role
        };

        Message accept = new Message(
            senderId: gameObject.name,
            conversationId: "assign-role",
            performative: Performative.Accept,
            content: content
        );

        CommunicationChannel.Instance.Publish(accept);
        Debug.Log($"{gameObject.name} asignó rol '{role}' a {receiverId}");
    }



}
