using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton pub-sub channel para que los agentes intercambien mensajes.
/// </summary>
public class CommunicationChannel : MonoBehaviour
{
    public static CommunicationChannel Instance { get; private set; }

    // Manejadores registrados por nombre del agente (ConversationId)
    private Dictionary<string, Action<Message>> subscribers = new();

    /// <summary>Se dispara cada vez que se publica un mensaje (opcional).</summary>
    public event Action<Message> OnMessagePublished;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // opcional
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Método para que un agente se suscriba con su ID.
    /// </summary>
    public void Subscribe(string conversationId, Action<Message> handler)
    {
        if (!subscribers.ContainsKey(conversationId))
        {
            subscribers.Add(conversationId, handler);
        }
        else
        {
            subscribers[conversationId] = handler; // sobrescribe si ya existía
        }
    }

    /// <summary>
    /// Envía un mensaje directamente al agente correspondiente.
    /// </summary>
    public void Publish(Message msg)
    {
        Debug.Log($"[Canal] Broadcast {msg.Performative} de {msg.SenderId} (conv: '{msg.ConversationId}')");
        OnMessagePublished?.Invoke(msg);
    }
}
