using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class policeActuator : MonoBehaviour
{
    private NavMeshAgent agent;
    public List<Transform> wayPoint; // Puntos de patrullaje

    public int currentwayPoint = 0;

    void Awake()
    {
        // Obtener el componente NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
    }

    // Mover al polic�a a un punto aleatorio en el NavMesh (cuando patrulla)
    public void Walking()
    {
        // Si no hay un destino pendiente y el polic�a ha llegado a su destino
        if (!agent.pathPending && agent.remainingDistance < 20f)
        {
            // Avanzar al siguiente punto de patrullaje
            currentwayPoint = (currentwayPoint + 1) % wayPoint.Count;
            agent.SetDestination(wayPoint[currentwayPoint].position);
        }
        MakeNoise();
    }

    // Mover al polic�a hacia el objetivo (por ejemplo, el ladr�n)
    public void MoveToTarget(Vector3 targetPosition)
    {
        agent.stoppingDistance = 0f;
        // Establecer el destino al que debe moverse el polic�a
        agent.SetDestination(targetPosition);
        MakeNoise();
    }

    //Detener al polic�a
    public void StopMoving()
    {
        agent.ResetPath(); // Detiene el movimiento
    }

    void MakeNoise()
    {
        // Encuentra todos los sensores de policías en la escena y les envía la detección de ruido
        policeSensor[] policeSensors = FindObjectsOfType<policeSensor>();
        foreach (policeSensor sensor in policeSensors)
        {
            if (sensor.gameObject != this.gameObject && sensor.gameObject.tag != "policia")
            {
               sensor.DetectNoise(transform.position);
            }       
        }
    }

    // Start se llama al inicio
    void Start()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();  // Aseg�rate de que el agente est� asignado
        }

        if (wayPoint.Count > 0)
        {
            agent.SetDestination(wayPoint[currentwayPoint].position); // Iniciar el patrullaje
        }
    }
}
