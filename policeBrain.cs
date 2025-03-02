using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class policeBrain : MonoBehaviour
{

    private policeSensor sensor;
    private policeActuator actuator;

    private enum PoliceState { Patrolling, Pursuing, Alert, Searching, VerifyTreasure, CampTreasure, CampDoor }
    private PoliceState currentState;
    private float searchTimer = 0f; // Temporizador de b�squeda
    private const float maxSearchTime = 15f; // Tiempo m�ximo en b�squeda antes de volver a patrullar
    //private bool endAction = false;
    private Dictionary<string, object> worldState;
    [SerializeField] private Transform doorWaypoint;
    [SerializeField] private Transform treasureRoomWaypoint;
    private bool searchPointSet = false;
    private bool searchPointSet_S = false;
    private Vector3 currentSearchPoint;
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
    void Update()
    {
        switch (currentState)
        {
            case PoliceState.Patrolling:
                Patrol();
                if ((bool)worldState["isThiefHeard"] && !(bool)worldState["isThiefSeen"])
                {
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
                if (!(bool)worldState["isThiefSeen"])
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
                    Debug.Log("cambiando de alerta a perseguir");
                    currentState = PoliceState.Pursuing;
                }
                else if (searchTimer >= maxSearchTime)
                {
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
                GoToTreasureRoom(); // añadir que al salir vueolva a cambiar su variable
                if ((bool)worldState["isThiefSeen"])
                {
                    UpdateState(WithTreasure: true);
                    currentState = PoliceState.Pursuing;
                }
                else if ((bool)worldState["isTreasureStolen"] && (bool)worldState["WithTreasure"])
                {
                    Debug.Log("Vigilaré la puerta");
                    UpdateState(WithTreasure: true);
                    currentState = PoliceState.CampDoor;
                }
                else if (!(bool)worldState["isTreasureStolen"] && (bool)worldState["WithTreasure"])
                {
                    

                    if (!HasReachedPatrolCheckpoint())
                    {
                        Debug.Log("Todavia no lluegue");
                        actuator.MoveToTarget(actuator.wayPoint[0].position);
                    }
                    else
                    {
                        Debug.Log("Lluegué a mi patrulla");
                        UpdateState(WithTreasure: true);
                        currentState = PoliceState.Patrolling;
                    }
                    // Debug.Log("Vuelvo a patrullar");
                    // currentState = PoliceState.Patrolling;
                   
                }
                break;

            case PoliceState.CampTreasure:
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
    }
    public void UpdateWorldState(string key, object value)
    {
        worldState[key] = value;
    }

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
    // M�todo para recibir la detecci�n de ruido con una zona aproximada.
    public void OnNoiseDetected(Vector3 zonaAproximada)
    {
        // Actualiza el estado del mundo: se ha escuchado un ruido y se asigna la zona aproximada.
        UpdateState(isThiefHeard: true, noisePosition: zonaAproximada);
        // Debug.Log("Ruido detectado en zona aproximada: " + zonaAproximada);
    }

    public void SomeoneSeen(bool detected, Vector3 detectedPosition)
    {
        UpdateState(isThiefSeen: detected, thiefPosition: detectedPosition);

    }
    void AlertState()
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
        
        Vector3 thiefPosition = (Vector3)worldState["thiefPosition"];
        actuator.MoveToTarget(thiefPosition); // Mueve al polic�a hacia el ladr�n
    }

    void Patrol()
    {
        actuator.Walking(); // L�gica de caminar mientras patrulla
    }

    void SearchForThief()
    {
        
        // L�gica para buscar al ladr�n en el �rea, por ejemplo, patrullando �reas cercanas
        // Si a�n no se ha definido un punto de b�squeda, se genera uno aleatorio
        if (!searchPointSet_S)
        {
            float searchRadius = 50f; // Define el radio de b�squeda alrededor de la �ltima posici�n conocida
            // Vector3 thiefPosition = (Vector3)worldState["thiefPosition"];
            currentSearchPoint = transform.position + new Vector3(Random.Range(-searchRadius, searchRadius), 0, Random.Range(-searchRadius, searchRadius));
            searchPointSet_S = true;
            Debug.Log("Buscando: Buscando en punto aleatorio: " + currentSearchPoint);
        }
        else
        {
            actuator.MoveToTarget(currentSearchPoint);
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f)
            {
                Debug.Log("Buscando: Ya llegué al punto");
                searchPointSet_S = false;
            }
        }
    }

    void GoToTreasureRoom()
    {
        actuator.MoveToTarget(treasureRoomWaypoint.position);
            // Si el policía ya llegó (o está muy cerca) del tesoro, se establece WithTreasure a true
        if (Vector3.Distance(transform.position, treasureRoomWaypoint.position) < 10f)
        {
            UpdateState(WithTreasure: true);
            Debug.Log("Llegado al tesoro, WithTreasure = true");
        }
    }
    // void StayAtTreasureRoom()
    // {

    // }
    void StayAtDoor()
    {
        actuator.MoveToTarget(doorWaypoint.position);
    }

    bool HasReachedPatrolCheckpoint()
    {
        float checkpointDistanceThreshold = 20f; // Distancia para considerar que llegó al checkpoint

        if (Vector3.Distance(transform.position, actuator.wayPoint[0].position) < checkpointDistanceThreshold)
        {
            return true;
        }
        
        return false;
    }

}


