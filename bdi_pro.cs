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
        if (assignedRole == "perseguir")
            currentIntention = "CatchThief";
        else if (assignedRole == "proteger_puerta")
            currentIntention = "ProtectDoor";
        else if (assignedRole == "cortar_camino")
            currentIntention = "Intercept";  // Este lo definimos ahora
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

        if (detected && currentCoordinatorId == null)
        {
            // Me autoproclamo coordinador
            isCoordinator = true;
            currentCoordinatorId = gameObject.name;
            Debug.Log($"{gameObject.name} se autoproclama COORDINADOR.");

            // Aviso a los demás quién es el coordinador
            Message msg = new Message(
                senderId: gameObject.name,
                conversationId: "new-coordinator",
                performative: Performative.Inform,
                content: gameObject.name
            );
            CommunicationChannel.Instance.Publish(msg);
            StartCoroutine(StartRoleAssignment());
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
        Vector3 thiefPos = (Vector3)worldState["thiefPosition"];
        Vector3 doorPos = doorWaypoint.position;

        // Punto intermedio entre ladrón y la puerta
        Vector3 interceptPoint = Vector3.Lerp(thiefPos, doorPos, 0.5f);

        actuator.MoveToTarget(interceptPoint);
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
    IEnumerator AssignRolesAfterDelay()
    {
        yield return new WaitForSeconds(2f); // Espera para recibir todas las propuestas

        // Obtenemos posición del ladrón
        Vector3 thiefPos = (Vector3)worldState["thiefPosition"];

        // Ordenamos policías por distancia al ladrón
        List<(string id, float distance)> distances = new List<(string, float)>();
        foreach (var entry in proposalsReceived)
        {
            float dist = Vector3.Distance(entry.Value, thiefPos);
            distances.Add((entry.Key, dist));
        }

        distances.Sort((a, b) => a.distance.CompareTo(b.distance));

        if (distances.Count >= 1)
        {
            SendAccept(distances[0].id, "perseguir");
        }
        if (distances.Count >= 2)
        {
            SendAccept(distances[1].id, "cortar_camino");
        }
        if (distances.Count >= 3)
        {
            SendAccept(distances[2].id, "proteger_puerta");
        }

        // Limpieza
        proposalsReceived.Clear();
        assigningRoles = false;
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
