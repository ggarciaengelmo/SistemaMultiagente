using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

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

    private policeSensor sensor;
    private policeActuator actuator;

    private enum PoliceState { Patrolling, Pursuing, Alert, Searching, VerifyTreasure, CampTreasure, CampDoor }
    private PoliceState currentState;
    private float searchTimer = 0f; // Temporizador de b�squeda
    private const float maxSearchTime = 10f; // Tiempo m�ximo en b�squeda antes de volver a patrullar
    //private bool endAction = false;
    private Dictionary<string, object> worldState;

    // para alert state
    private bool searchPointSet = false;
    private bool searchPointSet_S = false;
    private Vector3 currentSearchPoint;
    private bool isCoordinator = false;
    private string currentCoordinatorId = null;
    private string assignedRole = null;
    private bool assigningRoles = false;
    private Dictionary<string, Vector3> proposalsReceived = new Dictionary<string, Vector3>();

    // Waypoints históricos
    [SerializeField] private Transform doorWaypoint;
    [SerializeField] private Transform treasureRoomWaypoint;

    // Waypoints de habitaciones para la subasta
    [SerializeField] private Transform OxygenRoomWaypoint;

    [SerializeField] private Transform AdminRoomWaypoint;

    // Waypoints para interceptar
    [SerializeField] private Transform interceptEast1;
    [SerializeField] private Transform interceptEast2;
    [SerializeField] private Transform interceptWest1;
    [SerializeField] private Transform interceptWest2;
    [SerializeField] private Transform interceptNorth1;
    [SerializeField] private Transform interceptNorth2;

    // Waypoints específicos para la intercepción (asignados dinámicamente)
    private Transform interceptWaypoint1 = null;
    private Transform interceptWaypoint2 = null;

    void Awake()
    {
        // Buscar los componentes dentro del mismo GameObject
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

        currentState = PoliceState.Patrolling;

    }

    void Start()
    {
        // Esto garantiza que, siempre que el script esté activo, esté escuchando
        CommunicationChannel.Instance.OnMessagePublished += OnMessageReceived;
        Debug.Log($"{gameObject.name} se ha suscrito al canal");
    }


    void Update()
    {
        if (assignedRole == null)
        {
            switch (currentState)
            {
                case PoliceState.Patrolling:
                    Patrol();
                    if ((bool)worldState["isThiefHeard"] && !(bool)worldState["isThiefSeen"])
                    {
                        // Si no ha sido visto pero escuchado
                        Debug.Log("Estoy alerta");
                        currentState = PoliceState.Alert;
                    }
                    if ((bool)worldState["isThiefSeen"])
                    {
                        currentState = PoliceState.Pursuing;
                    }
                    break;

                case PoliceState.Pursuing:
                    PursueThief();
                    if (!(bool)worldState["isThiefSeen"]) // Cuando lo deje de ver
                    {
                        Debug.Log("cambiando de perseguir a buscar");
                        currentState = PoliceState.Searching;
                    }
                    break;

                case PoliceState.Alert:
                    AlertState();
                    searchTimer += Time.deltaTime;
                    if ((bool)worldState["isThiefSeen"])
                    {
                        searchTimer = 0;
                        Debug.Log("cambiando de alerta a perseguir");
                        currentState = PoliceState.Pursuing;
                    }
                    else if (searchTimer >= maxSearchTime) // Cuando lleve un tiempo alerta y no pasa nada...
                    {
                        searchTimer = 0;
                        Debug.Log("cambiando de alerta a verificar");
                        currentState = PoliceState.VerifyTreasure;
                    }
                    break;

                case PoliceState.Searching:
                    SearchForThief();
                    searchTimer += Time.deltaTime;

                    if ((bool)worldState["isThiefSeen"])
                    {
                        currentState = PoliceState.Pursuing;
                        searchTimer = 0f;
                    }
                    else if (searchTimer >= maxSearchTime)
                    {
                        // Tras verlo sabe si el tesoro ha sido robado
                        if ((bool)worldState["isTreasureStolen"])
                        {
                            Debug.Log("Vigilaré la puerta");
                            currentState = PoliceState.CampDoor;
                            searchTimer = 0f;
                        }
                        else
                        {
                            Debug.Log("Vigilaré el tesoro");
                            currentState = PoliceState.CampTreasure;
                            searchTimer = 0f;
                        }
                        break;
                    }
                    break;

                case PoliceState.VerifyTreasure:
                    GoToTreasureRoom();
                    if ((bool)worldState["isThiefSeen"])
                    {
                        UpdateState(WithTreasure: false);
                        currentState = PoliceState.Pursuing;
                    }
                    else if ((bool)worldState["isTreasureStolen"] && (bool)worldState["WithTreasure"]) // Si veo que ha sido robado
                    {
                        Debug.Log("Vigilaré la puerta");
                        UpdateState(WithTreasure: false);
                        currentState = PoliceState.CampDoor;
                    }
                    else if (!(bool)worldState["isTreasureStolen"] && (bool)worldState["WithTreasure"]) // Si no ha sido robado
                    {
                        Debug.Log("Me vuelvo a mi patrulla");
                        if (!HasReachedPatrolCheckpoint())
                        {
                            Debug.Log("Todavia no lluegue");
                            actuator.MoveToTarget(actuator.wayPoint[0].position);
                        }
                        else
                        {
                            Debug.Log("Lluegué a mi patrulla");
                            UpdateState(WithTreasure: false, isThiefHeard: false); // resetea el sonido
                            currentState = PoliceState.Patrolling;
                        }
                    }

                    break;

                case PoliceState.CampTreasure:
                    // Vete a la sala del tesoro y quedate allí (si está el tesoro)
                    GoToTreasureRoom();
                    if ((bool)worldState["isThiefSeen"])
                    {
                        currentState = PoliceState.Pursuing;
                    }
                    else if ((bool)worldState["isTreasureStolen"])
                    {
                        Debug.Log("Vigilaré la puerta");
                        currentState = PoliceState.CampDoor;
                    }
                    break;

                case PoliceState.CampDoor:
                    StayAtDoor();
                    if ((bool)worldState["isThiefSeen"])
                    {
                        currentState = PoliceState.Pursuing;
                    }
                    break;
            }
        } else {
            InterceptThief();
        }
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
        else if (assignedRole == "vigilar")
        {
            if ((bool)worldState["isTreasureStolen"])
            {
                actuator.MoveToTarget(doorWaypoint.position);
            }
            else
            {
                actuator.MoveToTarget(treasureRoomWaypoint.position);
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} no tiene asignado un waypoint de intercepción válido.");
        }
    }

    public void UpdateWorldState(string key, object value)
    {
        worldState[key] = value;
    }

    /// <summary>
    /// Actualiza el estado del mundo del policía.
    /// </summary>
    /// <param name="isThiefHeard"></param>
    /// <param name="isThiefSeen"></param>
    /// <param name="thiefPosition"></param>
    /// <param name="noisePosition"></param>
    /// <param name="isTreasureStolen"></param>
    /// <param name="WithTreasure"></param>
    public void UpdateState(bool? isThiefHeard = null, bool? isThiefSeen = null, Vector3? thiefPosition = null, Vector3? noisePosition = null, bool? isTreasureStolen = null, bool? WithTreasure = null)
    {
        if (isThiefHeard.HasValue)
        {
            UpdateWorldState("isThiefHeard", isThiefHeard.Value);
        }

        if (isThiefSeen.HasValue)
        {
            UpdateWorldState("isThiefSeen", isThiefSeen.Value);
        }

        if (thiefPosition.HasValue)
        {
            UpdateWorldState("thiefPosition", thiefPosition.Value);
        }

        if (noisePosition.HasValue)
        {
            UpdateWorldState("noisePosition", noisePosition.Value);
        }

        if (isTreasureStolen.HasValue)
        {
            UpdateWorldState("isTreasureStolen", isTreasureStolen.Value);
        }

        if (WithTreasure.HasValue)
        {
            UpdateWorldState("WithTreasure", WithTreasure.Value);
        }


    }

    /// <summary>
    /// M�todo para recibir la detecci�n de ruido con una zona aproximada. 
    /// </summary>
    /// <param name="zonaAproximada"></param>
    public void OnNoiseDetected(Vector3 zonaAproximada)
    {
        // Actualiza el estado del mundo: se ha escuchado un ruido y se asigna la zona aproximada.
        UpdateState(isThiefHeard: true, noisePosition: zonaAproximada);
        // Debug.Log("Ruido detectado en zona aproximada: " + zonaAproximada);
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

            // // Si ya tengo un rol asignado pero veo al ladrón, lo abandono
            // if (!isCoordinator && assignedRole != null)
            // {
            //     Debug.Log($"{gameObject.name} abandona el rol '{assignedRole}' para atrapar al ladrón.");
            //     assignedRole = null;
            // }

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

    /// <summary>
    /// Vigila la zona aproximada a partir de la última posición en la que escuchó un ruido.
    /// </summary>
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

    /// <summary>
    /// Vigila la zona aproximada a partir de la última posición que vió del ladrón
    /// </summary>
    void SearchForThief()
    {

        // L�gica para buscar al ladr�n en el �rea, por ejemplo, patrullando �reas cercanas
        // Si a�n no se ha definido un punto de b�squeda, se genera uno aleatorio
        if (!searchPointSet_S)
        {
            float searchRadius = 100f; // Define el radio de b�squeda alrededor de la �ltima posici�n conocida
            currentSearchPoint = transform.position + new Vector3(Random.Range(-searchRadius, searchRadius), 0, Random.Range(-searchRadius, searchRadius));
            searchPointSet_S = true;
            Debug.Log("Buscando: Buscando en punto aleatorio: " + currentSearchPoint);
            actuator.MoveToTarget(currentSearchPoint);
        }
        else
        {
            var navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();

            // Si el ladrón está cerca del punto o no se mueve (quedó bloqueado/ no puede alcanzarlo) --> Genera otro punto
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f || (navMeshAgent != null && navMeshAgent.velocity.magnitude == 0.0f))
            {
                Debug.Log("Buscando: Ya llegué al punto");
                searchPointSet_S = false;
            }
        }
    }

    /// <summary>
    /// Mueve el policia a la sala del tesoro para que sepa si ha sido robado o no.
    /// </summary>
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

    /// <summary>
    /// True si el policia está en algún punto de su patrulla, sirve para que en el camino de vuelta no se distraiga con ruidos
    /// </summary>
    /// <returns>Bool</returns>
    bool HasReachedPatrolCheckpoint()
    {
        float checkpointDistanceThreshold = 5f; // Distancia para considerar que llegó al checkpoint
        Vector3 posActual = transform.position;
        Vector3 posCheckpoint = actuator.wayPoint[0].position;
        posActual.y = 0;
        posCheckpoint.y = 0; // ignorar el eje y

        if (Vector3.Distance(posActual, posCheckpoint) < checkpointDistanceThreshold)
        {
            return true;
        }

        return false;
    }
    void OnTriggerEnter(Collider other)
    {
        // Comprobamos si el objeto con el que el policía colisiona tiene la etiqueta "Ladron"
        if (other.CompareTag("ladron"))
        {
            // El ladrón desaparece de la escena (desactiva su GameObject)
            other.gameObject.SetActive(false);

            Debug.Log("¡El policía ha atrapado al ladrón!");
        }
    }




    private void OnMessageReceived(Message msg)
    {
        Debug.Log($"{gameObject.name}: OnMessageReceived de {msg.SenderId}, performativa {msg.Performative}, convId '{msg.ConversationId}'");

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

    // Dependiendo del último lugar en el que se ha visto al ladrón, devuelve en que zona del mapa se encuentra
    private string CalculateThiefRelativePosition()
    {
        // Intentar obtener la posición del ladrón del estado del mundo
        if (worldState.TryGetValue("thiefPosition", out object thiefPosObj) && thiefPosObj is Vector3 thiefPos)
        {
            if (thiefPos.x > -77f)
            {
                // Si el ladrón está más a la derecha del almacén/cafetería
                return "East";
            }
            else if ((thiefPos.x < -600f) || (Vector3.Distance(thiefPos, OxygenRoomWaypoint.position) < 100f) && Vector3.Distance(thiefPos, AdminRoomWaypoint.position) > 110f)
            {
                // Si no está el ladrón en admin y está en oxygeno o más a la izquierda que comunicaciones
                return "West";
            }
            else if (thiefPos.z < 20f)
            {
                // Si no está en ningún sitio de los anteriores pero está más al Norte qeu Cafetería
                return "North";
            }
            else
            {
                // Está en cafetería (por descarte)
                return "South";
            }

        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No se pudo obtener una posición válida del ladrón para calcular la posición relativa.");
        }

        // Si no se cumple la condición o no hay posición válida
        return string.Empty;
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


        // 0: Se decide como será la subasta
        string thiefRelativePosition = CalculateThiefRelativePosition();
        Transform block1 = null; // Declarar block1
        Transform block2 = null; // Declarar block2


        if (thiefRelativePosition == "East")
        {
            block1 = interceptEast1;
            block2 = interceptEast2;
            Debug.Log($"{gameObject.name}: Ladrón detectado al Este. Usando waypoints East.");
        }
        else if (thiefRelativePosition == "West")
        {
            block1 = interceptWest1;
            block2 = interceptWest2;
            Debug.Log($"{gameObject.name}: Ladrón detectado al Oeste. Usando waypoints West.");
        }
        else if (thiefRelativePosition == "North")
        {
            block1 = interceptNorth1;
            block2 = interceptNorth2;
            Debug.Log($"{gameObject.name}: Ladrón detectado al Norte. Usando waypoints North.");
        }
        else if (thiefRelativePosition == "South")
        {
            if ((bool)worldState["isTreasureStolen"])
            {
                block1 = doorWaypoint; // Que no escape
                block2 = doorWaypoint;
            }
            else
            {
                block1 = treasureRoomWaypoint; // Que no robe
                block2 = doorWaypoint; // Si llega a robar, ya está vigilada la salida
            }
            Debug.Log($"{gameObject.name}: Ladrón detectado al Sur.");
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Error al asignar waypoints de intercepción (block1 o block2 son null). Zona: {thiefRelativePosition}");
            assigningRoles = false;
            yield break;
        }

        // actualizar variables globales
        interceptWaypoint1 = block1;
        interceptWaypoint2 = block2;

        // 1. Empieza la subasta una vez decida que va a ser
        List<(string id, string role)> roleAssignments = DetermineRoleAssignmentsByProximity(block1, block2);

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
        StartCoroutine(AssignRolesAfterDelay());
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