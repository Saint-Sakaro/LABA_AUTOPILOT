#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class ApplyMaterialsToRocks
{
    public static void ApplyRockMaterials()
    {
        CreateAndApplyRockMaterial();
    }

    private static void CreateAndApplyRockMaterial()
    {
        // Находим текстуры
        string[] diffuseGuids = AssetDatabase.FindAssets("rocky_terrain_02_diff_4k t:Texture2D");
        string[] normalGuids = AssetDatabase.FindAssets("rocky_terrain_02_nor_gl_4k");
        string[] roughGuids = AssetDatabase.FindAssets("rocky_terrain_02_rough_4k");

        if (diffuseGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найдена текстура rocky_terrain_02_diff_4k.jpg!", "OK");
            return;
        }

        string diffusePath = AssetDatabase.GUIDToAssetPath(diffuseGuids[0]);
        Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);

        Texture2D normalTex = null;
        if (normalGuids.Length > 0)
        {
            string normalPath = AssetDatabase.GUIDToAssetPath(normalGuids[0]);
            // EXR файлы могут быть загружены как Texture2D
            normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normalTex == null)
            {
                // Пытаемся загрузить как Texture
                Texture normalTexture = AssetDatabase.LoadAssetAtPath<Texture>(normalPath);
                if (normalTexture is Texture2D)
                {
                    normalTex = normalTexture as Texture2D;
                }
            }
        }

        Texture2D roughTex = null;
        if (roughGuids.Length > 0)
        {
            string roughPath = AssetDatabase.GUIDToAssetPath(roughGuids[0]);
            roughTex = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            if (roughTex == null)
            {
                Texture roughTexture = AssetDatabase.LoadAssetAtPath<Texture>(roughPath);
                if (roughTexture is Texture2D)
                {
                    roughTex = roughTexture as Texture2D;
                }
            }
        }

        // Создаем материал - используем правильный URP шейдер
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            // Пробуем альтернативные варианты
            urpLitShader = Shader.Find("Shader Graphs/Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                urpLitShader = Shader.Find("URP/Lit");
            }
        }
        
        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Ошибка", 
                "Не найден URP Lit шейдер! Убедитесь, что Universal Render Pipeline установлен в проекте.", 
                "OK");
            return;
        }
        
        Material rockMaterial = new Material(urpLitShader);
        rockMaterial.name = "RockMaterial";

        // Применяем текстуры
        if (diffuseTex != null)
        {
            rockMaterial.SetTexture("_BaseMap", diffuseTex);
        }

        if (normalTex != null)
        {
            rockMaterial.SetTexture("_BumpMap", normalTex);
            rockMaterial.EnableKeyword("_NORMALMAP");
        }

        if (roughTex != null)
        {
            rockMaterial.SetTexture("_MetallicGlossMap", roughTex);
            rockMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        // Сохраняем материал
        string materialPath = "Assets/Materials/RockMaterial.mat";
        AssetDatabase.CreateAsset(rockMaterial, materialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Загружаем сохраненный материал
        Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        // Применяем ко всем префабам камней
        int applied = 0;
        for (int i = 1; i <= 16; i++)
        {
            string prefabPath = $"Assets/Prefabs/Rocks/Rock_{i}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            if (prefab != null)
            {
                MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // Открываем префаб для редактирования
                    GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
                    MeshRenderer instanceRenderer = prefabInstance.GetComponent<MeshRenderer>();
                    
                    if (instanceRenderer != null)
                    {
                        instanceRenderer.material = savedMaterial;
                        PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                        applied++;
                    }
                    
                    PrefabUtility.UnloadPrefabContents(prefabInstance);
                }
            }
        }

        EditorUtility.DisplayDialog("Успех", 
            $"Материал создан и применен к {applied} префабам камней!\n\nМатериал сохранен в: {materialPath}", 
            "OK");
    }
}
#endif
