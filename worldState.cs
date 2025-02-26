using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class worldState : MonoBehaviour
{
    public bool isThiefSeen = false; // �El ladr�n ha sido visto?
    public bool isThiefHeard = false; // �El ladr�n ha sido escuchado?
    public bool isThiefCaught = false; // �El ladr�n ha sido atrapado?
    public Vector3 thiefPosition; // Posici�n del ladr�n

    // Actualiza los estados dependiendo de los eventos
    public void UpdateSeenStatus(bool seen, Vector3 position)
    {
        isThiefSeen = seen;
        if (seen)
        {
            thiefPosition = position; // Guarda la posici�n donde lo vio
        }
    }

    public void UpdateThiefHeard(bool heard)
    {
        isThiefHeard = heard;
    }

    public void UpdateThiefCaught(bool caught)
    {
        isThiefCaught = caught;
    }
}

