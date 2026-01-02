public sealed record ReportEmbeddingData(
    Guid ReportId,
    Guid WorkspaceId,
    string EmbedUrl,
    string EmbedToken);