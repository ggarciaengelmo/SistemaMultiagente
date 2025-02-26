using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;

    // Start is called before the first frame update
    void Start()
    {
        // Aseg�rate de que este objeto tiene un collider (puedes agregar un CapsuleCollider si es necesario)
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<CapsuleCollider>(); // A�ade un collider si no tiene uno
        }

        // Tambi�n aseg�rate de que est� en la capa correcta (por ejemplo, "Thief")
        gameObject.layer = LayerMask.NameToLayer("ladron");
    }

    // Update is called once per frame
    void Update()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveX, 0, moveZ) * speed * Time.deltaTime;
        transform.Translate(movement, Space.World);
    
        if (movement.magnitude > 0)
        {
            MakeNoise();
        }
    }

    void MakeNoise()
    {
        // Encuentra todos los sensores de policías en la escena y envía la detección de ruido
        policeSensor[] policeSensors = FindObjectsOfType<policeSensor>();
        foreach (policeSensor sensor in policeSensors)
        {
            sensor.DetectNoise(transform.position);
        }
    }
}
