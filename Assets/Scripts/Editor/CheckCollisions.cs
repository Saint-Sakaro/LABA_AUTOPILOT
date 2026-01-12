#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CheckCollisions
{
    [MenuItem("Tools/Проверить коллизии (деревья + камни + корабль)")]
    public static void CheckAllCollisions()
    {
        Debug.Log("=== Проверка коллизий ===");

        // 1. Проверяем корабль
        GameObject shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/v001/ship.prefab");
        if (shipPrefab == null)
        {
            Debug.LogWarning("Не найден префаб корабля!");
        }
        else
        {
            Debug.Log($"\n--- Корабль: {shipPrefab.name} ---");
            CheckPrefabColliders(shipPrefab, "Корабль");
        }

        // 2. Проверяем дерево
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tree.prefab");
        if (treePrefab == null)
        {
            Debug.LogWarning("Не найден префаб дерева!");
        }
        else
        {
            Debug.Log($"\n--- Дерево: {treePrefab.name} ---");
            CheckPrefabColliders(treePrefab, "Дерево");
        }

        // 3. Ищем все префабы камней
        string[] rockGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        List<GameObject> rockPrefabs = new List<GameObject>();
        
        foreach (string guid in rockGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && (prefab.name.Contains("Rock") || prefab.name.Contains("Stone")))
            {
                rockPrefabs.Add(prefab);
            }
        }

        if (rockPrefabs.Count > 0)
        {
            Debug.Log($"\n--- Найдено префабов камней: {rockPrefabs.Count} ---");
            for (int i = 0; i < Mathf.Min(3, rockPrefabs.Count); i++)
            {
                CheckPrefabColliders(rockPrefabs[i], $"Камень {i + 1}");
            }
            if (rockPrefabs.Count > 3)
            {
                Debug.Log($"... и еще {rockPrefabs.Count - 3} префабов");
            }
        }
        else
        {
            Debug.LogWarning("\nПрефабы камней не найдены!");
        }

        // 4. Проверяем Environment.prefab
        GameObject envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/v002/Environment.prefab");
        if (envPrefab != null)
        {
            Debug.Log($"\n--- Environment: {envPrefab.name} ---");
            CheckPrefabColliders(envPrefab, "Environment");
        }

        Debug.Log("\n=== Проверка завершена ===");
        EditorUtility.DisplayDialog("Проверка коллизий", 
            "Проверка завершена! Смотрите Console для подробностей.\n\n" +
            "Если у объектов есть коллайдеры с isTrigger = false и Rigidbody,\n" +
            "они будут физически взаимодействовать с кораблем.",
            "OK");
    }

    private static void CheckPrefabColliders(GameObject prefab, string name)
    {
        Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
        Rigidbody[] rigidbodies = prefab.GetComponentsInChildren<Rigidbody>(true);

        if (colliders.Length == 0)
        {
            Debug.Log($"{name}: ❌ Нет коллайдеров");
        }
        else
        {
            Debug.Log($"{name}: ✅ Найдено коллайдеров: {colliders.Length}");
            foreach (Collider col in colliders)
            {
                if (col == null) continue;
                string type = col.GetType().Name;
                string trigger = col.isTrigger ? "Trigger" : "Collider";
                string rigid = col.attachedRigidbody != null ? $" + Rigidbody (kinematic: {col.attachedRigidbody.isKinematic})" : "";
                Debug.Log($"  - {col.gameObject.name}: {type} ({trigger}){rigid}");
            }
        }

        if (rigidbodies.Length > 0)
        {
            Debug.Log($"{name}: ⚠️ Найдено Rigidbody: {rigidbodies.Length}");
            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null) continue;
                Debug.Log($"  - {rb.gameObject.name}: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}");
            }
        }
    }

    [MenuItem("Tools/Проверить коллизии в сцене")]
    public static void CheckSceneCollisions()
    {
        Debug.Log("=== Проверка коллизий в сцене ===");

        // Ищем корабль в сцене
        ShipController ship = Object.FindObjectOfType<ShipController>();
        if (ship != null)
        {
            Rigidbody shipRb = ship.GetComponent<Rigidbody>();
            Collider shipCol = ship.GetComponent<Collider>();
            
            Debug.Log($"\n--- Корабль в сцене ---");
            if (shipRb != null)
            {
                Debug.Log($"Rigidbody: isKinematic={shipRb.isKinematic}, useGravity={shipRb.useGravity}");
            }
            if (shipCol != null)
            {
                Debug.Log($"Collider: {shipCol.GetType().Name}, isTrigger={shipCol.isTrigger}");
            }
        }

        // Ищем деревья и камни в сцене
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
        int treeCount = 0;
        int rockCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Tree") || obj.name.Contains("tree"))
            {
                treeCount++;
                Collider col = obj.GetComponent<Collider>();
                if (col != null && !col.isTrigger)
                {
                    Debug.Log($"⚠️ Дерево в сцене '{obj.name}' имеет физический коллайдер!");
                }
            }
            if (obj.name.Contains("Rock") || obj.name.Contains("Stone"))
            {
                rockCount++;
                Collider col = obj.GetComponent<Collider>();
                if (col != null && !col.isTrigger)
                {
                    Debug.Log($"⚠️ Камень в сцене '{obj.name}' имеет физический коллайдер!");
                }
            }
        }

        Debug.Log($"\nДеревьев в сцене: {treeCount}");
        Debug.Log($"Камней в сцене: {rockCount}");
        Debug.Log("\n=== Проверка завершена ===");

        EditorUtility.DisplayDialog("Проверка сцены", 
            $"Проверка завершена!\n\n" +
            $"Деревьев: {treeCount}\n" +
            $"Камней: {rockCount}\n\n" +
            $"Смотрите Console для подробностей.",
            "OK");
    }
}
#endif
