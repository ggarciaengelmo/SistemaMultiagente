using System;
using UnityEngine;

/// <summary>
/// Tipos de performativas FIPA-ACL que usaremos.
/// </summary>
public enum Performative
{
    Inform,
    Request,
    Propose,
    Accept,
    Reject,
    Failure,
    Cfp
}

/// <summary>
/// Mensaje que viaja por el canal de comunicación.
/// </summary>
[Serializable]
public class Message
{
    public string SenderId;
    public string ConversationId;
    public Performative Performative;
    public object Content;
    public DateTime Timestamp;

    public Message(string senderId, string conversationId, Performative performative, object content)
    {
        SenderId = senderId;
        ConversationId = conversationId;
        Performative = performative;
        Content = content;
        Timestamp = DateTime.UtcNow;
    }
}
