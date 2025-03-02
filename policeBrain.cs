using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class policeBrain : MonoBehaviour
{

    private policeSensor sensor;
    private policeActuator actuator;

    private enum PoliceState { Patrolling, Pursuing, Alert, Searching, VerifyTreasure, CampTreasure, CampDoor }
    private PoliceState currentState;
    private float searchTimer = 0f; // Temporizador de búsqueda
    private const float maxSearchTime = 5f; // Tiempo máximo en búsqueda antes de volver a patrullar
    private bool endAction = false;
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
            {"thiefPosition", Vector3.zero}
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
                    currentState = PoliceState.Pursuing;
                }
                else if (endAction)
                {
                    currentState = PoliceState.VerifyTreasure;
                }
                break;

            case PoliceState.Searching:
                SearchForThief();
                searchTimer += Time.deltaTime;

                if ((bool)worldState["isThiefSeen"])
                {
                    Debug.Log("Cambiando de buscar a perseguir");
                    currentState = PoliceState.Pursuing;
                    searchTimer = 0f;
                }
                //else if (searchTimer >= maxSearchTime)
                //{
                //    if ((bool)worldState["isTreasureStolen"])
                //    {
                //        currentState = PoliceState.CampDoor;
                //        searchTimer = 0f;
                //    }
                //    else
                //    {
                //        currentState = PoliceState.CampTreasure;
                //        searchTimer = 0f;
                //    }
                //}
                break;


            case PoliceState.VerifyTreasure:
                GoToTreasureRoom();
                if ((bool)worldState["isThiefSeen"])
                {
                    currentState = PoliceState.Pursuing;
                }
                else if ((bool)worldState["isTreasureStolen"])
                {
                    currentState = PoliceState.CampDoor;
                }
                else if (!(bool)worldState["isTreasureStolen"])
                {
                    currentState = PoliceState.Patrolling;
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

    public void UpdateState(bool? isThiefHeard = null, bool? isThiefSeen = null, Vector3? thiefPosition = null, bool? isTreasureStolen = null)
    {
        // Si se proporciona un valor para "isThiefHeard", actualiza el estado
        if (isThiefHeard.HasValue)
        {
            UpdateWorldState("isThiefHeard", isThiefHeard.Value);
        }

        // Si se proporciona un valor para "isThiefSeen", actualiza el estado
        if (isThiefSeen.HasValue)
        {
            UpdateWorldState("isThiefSeen", isThiefSeen.Value);
        }

        // Si se proporciona un valor para la posición del ladrón, actualiza el estado
        if (thiefPosition.HasValue)
        {
            UpdateWorldState("thiefPosition", thiefPosition.Value);
        }

        // Si se proporciona un valor para "isTreasureStolen", actualiza el estado
        if (isTreasureStolen.HasValue)
        {
            UpdateWorldState("isTreasureStolen", isTreasureStolen.Value);
        }
    }
    // Método para recibir la detección de ruido con una zona aproximada.
    public void OnNoiseDetected(Vector3 zonaAproximada)
    {
        // Actualiza el estado del mundo: se ha escuchado un ruido y se asigna la zona aproximada.
        UpdateState(isThiefHeard: true, thiefPosition: zonaAproximada);
        //Debug.Log("Ruido detectado en zona aproximada: " + zonaAproximada);
    }

    public void SomeoneSeen(bool detected, Vector3 detectedPosition)
    {
        UpdateState(isThiefSeen: detected, thiefPosition: detectedPosition);

        if (detected)
        {
            //Debug.Log("🚨 Ladrón detectado en posición: " + detectedPosition);
        }
        else
        {
            Debug.Log("👀 No se ve al ladrón.");
        }

    }
    void AlertState()
    {
        Debug.Log("Buscando al ladrón...");
        // Lógica para buscar al ladrón en el área, por ejemplo, patrullando áreas cercanas
        // Si aún no se ha definido un punto de búsqueda, se genera uno aleatorio
        if (!searchPointSet)
        {
            float searchRadius = 10f; // Define el radio de búsqueda alrededor de la última posición conocida
            Vector3 thiefPosition = (Vector3)worldState["thiefPosition"];
            currentSearchPoint = thiefPosition + new Vector3(Random.Range(-searchRadius, searchRadius), 0, Random.Range(-searchRadius, searchRadius));
            searchPointSet = true;
            actuator.MoveToTarget(currentSearchPoint);
            //Debug.Log("Buscando en punto aleatorio: " + currentSearchPoint);
        }
        else
        {
            // Si el policía ya alcanzó el punto, se genera uno nuevo
            if (Vector3.Distance(transform.position, currentSearchPoint) < 2f)
            {
                searchPointSet = false;
            }

        }
    }

    void PursueThief()
    {
        Debug.Log("Persiguiendo...");
        Vector3 thiefPosition = (Vector3)worldState["thiefPosition"];
        actuator.MoveToTarget(thiefPosition); // Mueve al policía hacia el ladrón
    }

    void Patrol()
    {
        Debug.Log("Patrullando...");
        actuator.Walking(); // Lógica de caminar mientras patrulla
    }

    void SearchForThief()
    {
        Debug.Log("Buscando al ladrón...");

        if (!searchPointSet)
        {
            float searchRadius = Mathf.Min(20f, 10f + Time.timeSinceLevelLoad / 10f);
            Vector3 policePosition = transform.position; // Ahora usamos la posición actual del policía

            // Generamos un punto aleatorio en un círculo alrededor del policía
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(3f, searchRadius);
            float xOffset = Mathf.Cos(angle) * radius;
            float zOffset = Mathf.Sin(angle) * radius;

            currentSearchPoint = policePosition + new Vector3(xOffset, 0, zOffset);
            searchPointSet = true;

            //Debug.Log("Explorando alrededor de: " + policePosition + " - Punto de búsqueda: " + currentSearchPoint);

            actuator.MoveToTarget(currentSearchPoint);
        }
        else
        {
            float distance = Vector3.Distance(transform.position, currentSearchPoint);
            //Debug.Log("Distancia al punto de búsqueda: " + distance);

            if (distance < 1.5f) // Al alcanzar el punto, genera otro
            {
                //Debug.Log("Punto alcanzado. Siguiendo exploración...");
                searchPointSet = false;
            }
        }
    }




    void GoToTreasureRoom()
    {
        actuator.MoveToTarget(treasureRoomWaypoint.position);
    }
    void StayAtTreasureRoom()
    {

    }
    void StayAtDoor()
    {

    }


}


