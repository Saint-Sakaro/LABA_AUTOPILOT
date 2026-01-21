#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RefreshSceneObjectsMaterials
{
    [MenuItem("Tools/Обновить материалы объектов в сцене")]
    public static void RefreshSceneMaterials()
    {
        
        Material rockMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RockMaterial.mat");
        Material treeBarkMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TreeBarkMaterial_URP.mat");
        Material treeLeafMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TreeLeafMaterial_URP.mat");

        if (rockMaterial == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден материал RockMaterial! Сначала примените материалы к префабам.", "OK");
            return;
        }

        int rocksUpdated = 0;
        int treesUpdated = 0;

        
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            
            if (obj.name.StartsWith("Rock_") || obj.name.Contains("Rock"))
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = rockMaterial;
                    rocksUpdated++;
                }
            }

            
            if (obj.name.Contains("Tree") || obj.name.Contains("tree"))
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    
                    if (treeBarkMaterial != null && treeLeafMaterial != null)
                    {
                        Material[] materials = new Material[2];
                        materials[0] = treeBarkMaterial;
                        materials[1] = treeLeafMaterial;
                        renderer.materials = materials;
                    }
                    else if (treeBarkMaterial != null)
                    {
                        renderer.material = treeBarkMaterial;
                    }
                    treesUpdated++;
                }
            }
        }

        EditorUtility.DisplayDialog("Успех", 
            $"Материалы обновлены в сцене!\n\n" +
            $"Камней обновлено: {rocksUpdated}\n" +
            $"Деревьев обновлено: {treesUpdated}\n\n" +
            $"Если объекты все еще розовые, попробуйте:\n" +
            $"1. Удалить все объекты в сцене\n" +
            $"2. Перезапустить игру (Play)\n" +
            $"3. PlatformGenerator создаст новые объекты с правильными материалами",
            "OK");
    }

    [MenuItem("Tools/Удалить все деревья и камни из сцены")]
    public static void ClearSceneObjects()
    {
        if (!EditorUtility.DisplayDialog("Подтверждение", 
            "Вы уверены, что хотите удалить все деревья и камни из текущей сцены?\n\n" +
            "Это удалит все объекты с именами, содержащими 'Rock' или 'Tree'.",
            "Да, удалить", "Отмена"))
        {
            return;
        }

        int deleted = 0;
        GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();

        List<GameObject> toDelete = new List<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.StartsWith("Rock_") || obj.name.Contains("Rock") || 
                obj.name.Contains("Tree") || obj.name.Contains("tree"))
            {
                
                if (obj.transform.parent != null && 
                    !obj.name.Contains("PlatformGenerator") &&
                    !AssetDatabase.Contains(obj))
                {
                    toDelete.Add(obj);
                }
            }
        }

        foreach (GameObject obj in toDelete)
        {
            Object.DestroyImmediate(obj);
            deleted++;
        }

        EditorUtility.DisplayDialog("Успех", 
            $"Удалено объектов: {deleted}\n\n" +
            $"Теперь перезапустите игру (Play), и PlatformGenerator создаст новые объекты с правильными материалами.",
            "OK");
    }
}
#endif
