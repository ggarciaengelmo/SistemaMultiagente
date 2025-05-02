using System;
using UnityEditor.VersionControl;
using UnityEngine;

/// <summary>
/// Singleton pub-sub channel para que los agentes intercambien mensajes.
/// </summary>
public class CommunicationChannel : MonoBehaviour
{
    public static CommunicationChannel Instance { get; private set; }

    /// <summary>Se dispara cada vez que se publica un mensaje.</summary>
    public event Action<Message> OnMessagePublished;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Opcional: mantener vivo entre escenas
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Publica un mensaje a todos los suscriptores.
    /// </summary>
    public void Publish(Message msg)
    {
        OnMessagePublished?.Invoke(msg);
    }
}
