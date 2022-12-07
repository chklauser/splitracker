﻿using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace Splitracker.Persistence;

public class DataAccessControlException : Exception
{
    [PublicAPI]
    public string DocumentId { get; }
    [PublicAPI]
    public string UserId { get; }

    public DataAccessControlException(string documentId, string userId)
    {
        DocumentId = documentId;
        UserId = userId;
    }

    protected DataAccessControlException(SerializationInfo info, StreamingContext context
    ) : base(info, context)
    {
        DocumentId = info.GetString(nameof(DocumentId)) ?? string.Empty;
        UserId = info.GetString(nameof(UserId)) ?? string.Empty;
    }

    public DataAccessControlException(string? message, string documentId, string userId) : base(message)
    {
        DocumentId = documentId;
        UserId = userId;
    }

    public DataAccessControlException(string? message, Exception? innerException, string documentId,
        string userId
    ) : base(message, innerException)
    {
        DocumentId = documentId;
        UserId = userId;
    }
}