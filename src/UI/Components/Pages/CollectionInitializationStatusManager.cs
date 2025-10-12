using System.Collections.Concurrent;

namespace UI.Components.Pages;

public enum InitializationStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

public sealed class CollectionInitializationInfo
{
    public string CollectionName { get; private set; }
    public InitializationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public CollectionInitializationInfo(string collectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        CollectionName = collectionName;
        Status = InitializationStatus.NotStarted;
    }

    public void SetStatus(InitializationStatus status, string? errorMessage = null)
    {
        if (status == InitializationStatus.NotStarted)
        {
            throw new ArgumentException($"Can not set the status to '{nameof(InitializationStatus.NotStarted)}'.'");
        }

        Status = status;

        switch (status)
        {
            case InitializationStatus.InProgress:
                StartedAt = DateTime.UtcNow;

                break;
            case InitializationStatus.Completed:
                CompletedAt = DateTime.UtcNow;

                break;
            case InitializationStatus.Failed:
                CompletedAt = DateTime.UtcNow;
                ErrorMessage = errorMessage;

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }
}

public sealed class CollectionInitializationStatusManager
{
    private readonly ConcurrentDictionary<string, CollectionInitializationInfo> _statuses = new();

    public event EventHandler<CollectionInitializationInfo>? CollectionStatusChanged;

    public CollectionInitializationInfo GetCollectionInitializationInfo(string collectionName) => _statuses.GetOrAdd(collectionName,
                                                                                                                     name => new CollectionInitializationInfo(name));

    public void SetCollectionStatus(string collectionName, InitializationStatus status, string? errorMessage = null)
    {
        CollectionInitializationInfo info = _statuses.GetOrAdd(collectionName, name => new CollectionInitializationInfo(name));

        info.SetStatus(status, errorMessage);

        CollectionStatusChanged?.Invoke(sender: this, info);
    }
}