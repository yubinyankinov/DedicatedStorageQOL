using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using Newtonsoft.Json.Linq;

[BepInPlugin("YubinYankinov.DedicatedStorageQOL", "DedicatedStorageQOL", "1.0.3")]
public class DedicatedStorageQOL : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private static ConfigEntry<float> TransferRadius;
    private static ConfigEntry<KeyCode> ModKey1;
    private static ConfigEntry<KeyCode> ModKey2;

    private const string GitHubOwner = "yubinyankinov";
    private const string GitHubRepo = "DedicatedStorageQOL";

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
        var player = Player.m_localPlayer;
        if (player == null) return;

        if (ZInput.GetKey(ModKey1.Value) && ZInput.GetKeyDown(ModKey2.Value))
        {
            TryTransferToNearbyStorage(player);
        }
    }

    private void TryTransferToNearbyStorage(Player player)
    {
        GameObject hoveredObj = player.GetHoverObject();
        Container hoveredContainer = hoveredObj?.GetComponentInParent<Container>();

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

        if (transferCount > 0)
        {
            string message = $"<color=#00FF00>✅ Transferred {transferCount} item{(transferCount > 1 ? "s" : "")}!</color>";
            StartCoroutine(ShowAnimatedMessage(player, message));
        }
    }

    private System.Collections.IEnumerator ShowAnimatedMessage(Player player, string message)
    {
        float delay = 0.15f;
        int repeat = 3;
        for (int i = 0; i < repeat; i++)
        {
            player.Message(MessageHud.MessageType.Center, message);
            yield return new WaitForSeconds(delay);
            player.Message(MessageHud.MessageType.Center, "");
            yield return new WaitForSeconds(delay);
        }

        player.Message(MessageHud.MessageType.Center, message);
        yield return new WaitForSeconds(2f);
        player.Message(MessageHud.MessageType.Center, "");
    }

}
