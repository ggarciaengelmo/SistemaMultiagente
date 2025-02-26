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
    private const float maxSearchTime = 7f; // Tiempo máximo en búsqueda antes de volver a patrullar
    private bool endAction = false;
    private Dictionary<string, object> worldState;

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
                else if ((bool)worldState["isThiefSeen"])
                {
                    currentState = PoliceState.Pursuing;
                }
                break;

            case PoliceState.Pursuing:
                PursueThief();
                if (!(bool)worldState["isThiefSeen"])
                {
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
                    currentState = PoliceState.Pursuing;
                    searchTimer = 0f;
                }
                else if (searchTimer >= maxSearchTime)
                {
                    if ((bool)worldState["isTreasureStolen"])
                    {
                        currentState = PoliceState.CampDoor;
                        searchTimer = 0f;
                    }
                    else
                    {
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
                    currentState = PoliceState.CampDoor;
                } 
                else if (!(bool)worldState["isTreaasureStolen"]) 
                {

                    currentState = PoliceState.Patrolling;
                }
                break;

            case PoliceState.CampTreasure:
                StayAtTreasureRoom();
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
        worldState.isThiefHeard = true;
        worldState.noiseZone = zonaAproximada;
        Debug.Log("Ruido detectado en zona aproximada: " + zonaAproximada);
    }


    void AlertState()
    {
        Debug.Log("Policía en estado de alerta, buscando al ladrón.");
        // Lógica para buscar al ladrón o actuar en estado de alerta
    }

    void PursueThief()
    {
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
        // Lógica para buscar al ladrón en el área, por ejemplo, patrullando áreas cercanas
    }
    
    void GoToTreasureRoom()
    {

    }
    void StayAtTreasureRoom()
    {

    }
    void StayAtDoor()
    {

    }


}


