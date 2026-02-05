using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Machines;

public class FirstRoomUpgradeStationSetup
{
    [MenuItem("Tools/Beneath The Floor/Setup First Room Upgrade Station")]
    public static void SetupUpgradeStation()
    {
        // Find the Upgrade_Station_FirstRoom object
        GameObject upgradeStation = GameObject.Find("Upgrade_Station_FirstRoom");

        if (upgradeStation == null)
        {
            Debug.LogError("[FirstRoomUpgradeStationSetup] Could not find 'Upgrade_Station_FirstRoom' in scene!");
            return;
        }

        // Check if it already has the component
        var existingComponent = upgradeStation.GetComponent<FirstRoomUpgradeStation>();
        if (existingComponent != null)
        {
            Debug.Log("[FirstRoomUpgradeStationSetup] FirstRoomUpgradeStation component already exists.");
            Selection.activeGameObject = upgradeStation;
            return;
        }

        // Add the FirstRoomUpgradeStation component
        var station = upgradeStation.AddComponent<FirstRoomUpgradeStation>();

        // Add BoxCollider if missing
        var collider = upgradeStation.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = upgradeStation.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(2f, 2f, 2f);
            collider.center = new Vector3(0f, 1f, 0f);
        }

        // Mark as dirty to save changes
        EditorUtility.SetDirty(upgradeStation);

        Debug.Log("[FirstRoomUpgradeStationSetup] Successfully added FirstRoomUpgradeStation component!");
        Debug.Log("[FirstRoomUpgradeStationSetup] The station requires the engine to be activated (crystal inserted) to use.");
        Selection.activeGameObject = upgradeStation;
    }
}
