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
            {"noisePosition", Vector3.zero}
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
                else if (!(bool)worldState["isTreasureStolen"])
                {
                    // // Si aún no ha alcanzado un checkpoint, moverse a uno
                    if (!HasReachedPatrolCheckpoint())
                    {
                        actuator.MoveToTarget(actuator.wayPoint[0].position);
                    }
                    else
                    {
                        Debug.Log("Lluegué a mi patrulla");
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

    public void UpdateState(bool? isThiefHeard = null, bool? isThiefSeen = null, Vector3? thiefPosition = null, Vector3? noisePosition = null, bool? isTreasureStolen = null)
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
    }
    // M�todo para recibir la detecci�n de ruido con una zona aproximada.
    public void OnNoiseDetected(Vector3 zonaAproximada)
    {
        // Actualiza el estado del mundo: se ha escuchado un ruido y se asigna la zona aproximada.
        UpdateState(isThiefHeard: true, noisePosition: zonaAproximada);
        Debug.Log("Ruido detectado en zona aproximada: " + zonaAproximada);
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
            Debug.Log("Buscando en punto aleatorio: " + currentSearchPoint);
        }
        else
        {
            // Si el polic�a ya alcanz� el punto o se para porque no puede alcanzarlo, se genera uno nuevo
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f || GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.magnitude == 0.0f)
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
        if (!searchPointSetB)
        {
            float searchRadius = 50f; // Define el radio de b�squeda alrededor de la �ltima posici�n conocida
            Vector3 thiefPosition = (Vector3)worldState["thiefPosition"];
            currentSearchPoint = thiefPosition + new Vector3(Random.Range(-searchRadius, searchRadius), 0, Random.Range(-searchRadius, searchRadius));
            searchPointSetB = true;
            actuator.MoveToTarget(currentSearchPoint);
            Debug.Log("Buscando en punto aleatorio: " + currentSearchPoint);
        }
        else
        {
            // Si el polic�a ya alcanz� el punto o se para porque no puede alcanzarlo, se genera uno nuevo
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f || GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.magnitude == 0.0f)
            {
                searchPointSetB = false;
            }
        }
    }

    void GoToTreasureRoom()
    {
        actuator.MoveToTarget(treasureRoomWaypoint.position);
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
        float checkpointDistanceThreshold = 2f; // Distancia para considerar que llegó al checkpoint
        Vector3 currentPosition = transform.position;

        foreach (Transform checkpoint in actuator.wayPoint)
        {
            if (Vector3.Distance(currentPosition, checkpoint.position) < checkpointDistanceThreshold)
            {
                return true;
            }
        }
        return false;
    }

}


