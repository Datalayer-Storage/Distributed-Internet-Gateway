namespace dig;

[Command("stores", Description = "Manage subscriptions and mirrors.")]
internal sealed class StoreCommands
{
    [Command("add", Description = "Subscribe to a store and create a server coin.")]
    public AddStoreCommand Add { get; init; } = new();

    [Command("remove", Description = "Unsubscribe from a store and delete its coin.")]
    public RemoveStoreCommand Remove { get; init; } = new();

    [Command("unsubscribe-all", Description = "Unsubscribe from all stores and delete their coins.")]
    public UnsubscribeAllCommand CheckHost { get; init; } = new();

    [Command("unmirror-all", Description = "Unmirror all subscribed stores.")]
    public UnmirrorAllCommand Unmirror { get; init; } = new();

    [Command("list", Description = "List subscribed stores, their mirrors and coins.")]
    public ListAllCommand List { get; init; } = new();

    [Command("sync", Description = "Syncs the DIG node with the data layer.")]
    public SyncStoresCommand Sync { get; init; } = new();

    [Command("check-fee", Description = "Check the fee for adding a mirror or coin.")]
    public CheckFeeCommand CheckFee { get; init; } = new();
}
