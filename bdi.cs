using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
}
