using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[BepInPlugin("com.yourname.DedicatedStorageQOL", "DedicatedStorageQOL", "1.0.0")]
[BepInDependency("com.xElemental.ConfigurationManager", BepInDependency.DependencyFlags.SoftDependency)]
public class DedicatedStorageQOL : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private static ConfigEntry<float> TransferRadius;
    private static ConfigEntry<KeyCode> ModKey1;
    private static ConfigEntry<KeyCode> ModKey2;

    private void Awake()
    {
        Logger = base.Logger;

        // Config entries
        TransferRadius = Config.Bind("General", "TransferRadius", 10f, "Max distance (meters) to scan for storage boxes.");
        ModKey1 = Config.Bind("Hotkeys", "PrimaryKey", KeyCode.LeftControl, "First modifier key for quick transfer.");
        ModKey2 = Config.Bind("Hotkeys", "SecondaryKey", KeyCode.E, "Second modifier key for quick transfer.");

        Logger.LogInfo("DedicatedStorageQOL loaded successfully!");

    }

    private void Update()
    {
        // Only act if player exists
        var player = Player.m_localPlayer;
        if (player == null) return;

        // Check key combination
        if (ZInput.GetKey(ModKey1.Value) && ZInput.GetKeyDown(ModKey2.Value))
        {
            TryTransferToNearbyStorage(player);
        }
    }

    private void TryTransferToNearbyStorage(Player player)
    {
        // Hovered container
        GameObject hoveredObj = player.GetHoverObject();
        Container hoveredContainer = hoveredObj?.GetComponentInParent<Container>();

        // Nearby containers
        Vector3 center = player.transform.position;
        Collider[] colliders = UnityEngine.Physics.OverlapSphere(center, TransferRadius.Value);

        List<Container> containers = new List<Container>();
        foreach (var col in colliders)
        {
            Container c = col.GetComponentInParent<Container>();
            if (c != null)
            {
                var nview = c.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                    containers.Add(c);
            }
        }

        // Prioritize hovered container
        if (hoveredContainer != null && !containers.Contains(hoveredContainer))
            containers.Add(hoveredContainer);

        AutoTransferItems(player, containers);
    }

    private void AutoTransferItems(Player player, List<Container> containers)
    {
        if (player == null || containers == null || containers.Count == 0) return;

        Inventory playerInventory = player.GetInventory();
        if (playerInventory == null) return;

        List<ItemDrop.ItemData> itemsToTransfer = new List<ItemDrop.ItemData>(playerInventory.GetAllItems());
        int transferCount = 0;

        foreach (var item in itemsToTransfer)
        {
            bool transferred = false;

            foreach (var container in containers)
            {
                Inventory containerInv = container.GetInventory();
                if (containerInv == null) continue;

                // Only transfer if container has same item type
                if (containerInv.GetAllItems().Any(i => i.m_shared.m_name == item.m_shared.m_name))
                {
                    ItemDrop.ItemData clone = item.Clone();
                    clone.m_stack = item.m_stack;

                    if (containerInv.AddItem(clone))
                    {
                        playerInventory.RemoveItem(item);
                        transferred = true;
                        transferCount++;
                        break;
                    }
                }
            }
        }

        // Sort inventories
        SortInventory(playerInventory);
        foreach (var container in containers)
            SortInventory(container.GetInventory());

        // Show animated success message
        if (transferCount > 0)
        {
            string message = $"<color=#00FF00>✅ Transferred {transferCount} item{(transferCount > 1 ? "s" : "")}!</color>";
            StartCoroutine(ShowAnimatedMessage(player, message));
        }
    }

    private System.Collections.IEnumerator ShowAnimatedMessage(Player player, string message)
    {
        // Animation: fade in/out by repeating the message 3 times quickly
        float delay = 0.15f;
        int repeat = 3;
        for (int i = 0; i < repeat; i++)
        {
            player.Message(MessageHud.MessageType.Center, message);
            yield return new WaitForSeconds(delay);
            player.Message(MessageHud.MessageType.Center, ""); // Clear for fade effect
            yield return new WaitForSeconds(delay);
        }

        // Final message stays for 2 seconds
        player.Message(MessageHud.MessageType.Center, message);
        yield return new WaitForSeconds(2f);
        player.Message(MessageHud.MessageType.Center, "");
    }

    private void SortInventory(Inventory inv)
    {
        if (inv == null) return;

        var sortedItems = inv.GetAllItems().OrderBy(i => i.m_shared.m_name).ToList();
        inv.RemoveAll();

        foreach (var item in sortedItems)
        {
            ItemDrop.ItemData clone = item.Clone();
            clone.m_stack = item.m_stack;
            inv.AddItem(clone);
        }
    }

}