using UnityEngine;
using UnityEditor;
using BeneathTheFloor.Economy;

public class FirstRoomSellStationSetup
{
    [MenuItem("Tools/Beneath The Floor/Setup First Room Sell Station")]
    public static void SetupSellStation()
    {
        // Find the Sell_Station_FirstRoom object
        GameObject sellStation = GameObject.Find("Sell_Station_FirstRoom");

        if (sellStation == null)
        {
            Debug.LogError("[FirstRoomSellStationSetup] Could not find 'Sell_Station_FirstRoom' in scene!");
            return;
        }

        // Check if it already has the component
        var existingComponent = sellStation.GetComponent<FirstRoomSellStation>();
        if (existingComponent != null)
        {
            Debug.Log("[FirstRoomSellStationSetup] FirstRoomSellStation component already exists.");
            Selection.activeGameObject = sellStation;
            return;
        }

        // Add the FirstRoomSellStation component
        var station = sellStation.AddComponent<FirstRoomSellStation>();

        // Add BoxCollider if missing
        var collider = sellStation.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = sellStation.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.size = new Vector3(10f, 5f, 10f); // Adjust based on visual size
            collider.center = new Vector3(0f, 2.5f, 0f);
        }

        // Mark as dirty to save changes
        EditorUtility.SetDirty(sellStation);

        Debug.Log("[FirstRoomSellStationSetup] Successfully added FirstRoomSellStation component to Sell_Station_FirstRoom!");
        Selection.activeGameObject = sellStation;
    }
}
