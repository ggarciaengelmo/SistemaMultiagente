using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class policeSensor : MonoBehaviour
{
    public float detectionRadius = 10f; // Distancia m�xima de visi�n
    public float fieldOfView = 90f; // �ngulo del campo de visi�n
    public int numRays = 10; // N�mero de rayos a lanzar

    public LayerMask targetMask; // Para detectar solo al ladr�n
    public LayerMask obstacleMask; // Para detectar obst�culos

    private policeBrain policeBrain;

    // Start is called before the first frame update
    void Start()
    {
        policeBrain = GetComponent<policeBrain>(); // Asumiendo que policeBrain est� en el mismo GameObject
        StartCoroutine(DetectRoutine());
    }

    IEnumerator DetectRoutine()
    {
        while (true)
        {
            Detect();
            yield return new WaitForSeconds(0.1f); // Detecta cada 0.1 segundos
        }
    }

    void Detect()
    {
        int rays = numRays;
        float angleStep = fieldOfView / rays;

        bool detected = false;
        Vector3 detectedPosition = Vector3.zero;

        for (int i = 0; i < rays; i++)
        {
            // Calculamos el ángulo de cada rayo
            float angle = -fieldOfView / 2 + i * angleStep;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Dibujamos el rayo siempre, aunque no detecte nada
            Color rayColor = Color.red; // Rojo por defecto (no detecta nada)

            // Lanzamos el rayo y verificamos si hay un obstáculo o si detecta al ladrón
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, detectionRadius))
            {
                // Si el rayo choca con un obstáculo antes de llegar al ladrón, no se detecta
                if (((1 << hit.collider.gameObject.layer) & obstacleMask) != 0)
                {
                    rayColor = Color.blue; // Obstáculo detectado, el rayo se bloquea
                }
                else if (((1 << hit.collider.gameObject.layer) & targetMask) != 0)
                {
                    // Solo detecta al ladrón si no hay obstáculos en el medio
                    rayColor = Color.green; // Detectó al ladrón
                    detected = true;
                    detectedPosition = hit.point;
                    Debug.Log("Ladrón detectado en la posición: " + hit.point);
                }
            }

            // Dibujamos el rayo con el color correspondiente
            Debug.DrawRay(transform.position, direction * detectionRadius, rayColor);
        }

        // Actualizamos el estado con la información de si se detectó algo
        policeBrain.SomeoneSeen(detected, detectedPosition);
    }

    // M�todo para dibujar el campo de visi�n del polic�a
    void DrawFieldOfView()
    {
        int rays = numRays;
        float angleStep = fieldOfView / rays;
        Vector3 lastPoint = transform.position;

        for (int i = 0; i < rays; i++)
        {
            // Calculamos el �ngulo de cada rayo
            float angle = -fieldOfView / 2 + i * angleStep;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Dibujamos la l�nea que representa el campo de visi�n
            Vector3 endPoint = transform.position + direction * detectionRadius;
            Debug.DrawLine(lastPoint, endPoint, Color.yellow); // Dibuja la l�nea en amarillo para representar el campo de visi�n
            lastPoint = endPoint;
        }
    }

    // Llamamos a la funci�n DrawFieldOfView para dibujar el campo de visi�n en cada actualizaci�n
    void Update()
    {
        DrawFieldOfView(); // Dibuja el campo de visi�n
    }

    public float noiseDetectionRadius = 10f; // Radio fijo en el que se puede detectar el ruido
    public float noiseMarginError = 30f; // Margen de error para aproximar la zona del ruido

    private policeBrain brain;

    // Método para detectar ruido
    public void DetectNoise(Vector3 noiseOrigin)
    {
        // Genera una zona aproximada añadiendo un pequeño margen de error
        Vector3 approximateZone = noiseOrigin + new Vector3(
            Random.Range(-noiseMarginError, noiseMarginError),
            0,
            Random.Range(-noiseMarginError, noiseMarginError)
        );

        // Verificar si este policía está dentro del radio de detección
        float distance = Vector3.Distance(transform.position, noiseOrigin);
        if (distance <= noiseDetectionRadius)
        {
            brain.OnNoiseDetected(approximateZone);
            Debug.Log("se ha escuchado algo");
        }
    }
    
    void Awake()
    {
        brain = GetComponent<policeBrain>();
    }
}
