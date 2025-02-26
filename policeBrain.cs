using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class policeBrain : MonoBehaviour
{
    private worldState worldState;
    private policeSensor sensor;
    private policeActuator actuator;

    private enum PoliceState { Patrolling, Pursuing, Alert, Searching }
    private PoliceState currentState;
    private float searchTimer = 0f; // Temporizador de b�squeda
    private const float maxSearchTime = 7f; // Tiempo m�ximo en b�squeda antes de volver a patrullar


    void Awake()
    {
        // Buscar los componentes dentro del mismo GameObject
        worldState = GetComponent<worldState>();
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
                if (worldState.isThiefHeard && !worldState.isThiefSeen)
                {
                    currentState = PoliceState.Alert;
                }
                else if (worldState.isThiefSeen)
                {
                    currentState = PoliceState.Pursuing;
                }
                break;

            case PoliceState.Pursuing:
                PursueThief();
                if (!worldState.isThiefSeen)
                {
                    currentState = PoliceState.Searching;
                }
                break;

            case PoliceState.Alert:
                AlertState();
                if (worldState.isThiefSeen)
                {
                    currentState = PoliceState.Pursuing;
                }
                else if (!worldState.isThiefHeard)
                {
                    currentState = PoliceState.Patrolling;
                }
                break;

            case PoliceState.Searching:
                SearchForThief();
                searchTimer += Time.deltaTime;
                if (worldState.isThiefSeen)
                {
                    currentState = PoliceState.Pursuing;
                    searchTimer = 0f;
                }
                else if (worldState.isThiefHeard)
                {
                     currentState = PoliceState.Alert;
                     searchTimer = 0f;
                }
                else if (searchTimer >= maxSearchTime)
                {
                    currentState = PoliceState.Patrolling;
                    searchTimer = 0f;
                }
                break;
        }
    }


    void AlertState()
    {
        Debug.Log("Polic�a en estado de alerta, buscando al ladr�n.");
        // L�gica para buscar al ladr�n o actuar en estado de alerta
    }

    void PursueThief()
    {
        Vector3 thiefPosition = worldState.thiefPosition;
        actuator.MoveToTarget(thiefPosition); // Mueve al polic�a hacia el ladr�n
    }

    void Patrol()
    {
        Debug.Log("Patrullando...");
        actuator.Walking(); // L�gica de caminar mientras patrulla
    }

    void SearchForThief()
    {
        Debug.Log("Buscando al ladr�n...");
        // L�gica para buscar al ladr�n en el �rea, por ejemplo, patrullando �reas cercanas
    }
}
