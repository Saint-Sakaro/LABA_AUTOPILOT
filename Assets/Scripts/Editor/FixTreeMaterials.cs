#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class FixTreeMaterials
{
    [MenuItem("Tools/Fix Tree Materials for URP")]
    public static void FixTreeMaterialsForURP()
    {
        string treePrefabPath = "Assets/Tree.prefab";
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePrefabPath);
        
        if (treePrefab == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найден префаб Tree.prefab!", "OK");
            return;
        }

        // Находим текстуры
        string[] diffuseGuids = AssetDatabase.FindAssets("diffuse t:Texture2D", new[] { "Assets/Tree_Textures" });
        string[] normalGuids = AssetDatabase.FindAssets("normal_specular t:Texture2D", new[] { "Assets/Tree_Textures" });
        string[] shadowGuids = AssetDatabase.FindAssets("shadow t:Texture2D", new[] { "Assets/Tree_Textures" });
        string[] translucencyGuids = AssetDatabase.FindAssets("translucency_gloss t:Texture2D", new[] { "Assets/Tree_Textures" });

        if (diffuseGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не найдена текстура diffuse.png в Assets/Tree_Textures!", "OK");
            return;
        }

        string diffusePath = AssetDatabase.GUIDToAssetPath(diffuseGuids[0]);
        Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);

        Texture2D normalTex = null;
        if (normalGuids.Length > 0)
        {
            string normalPath = AssetDatabase.GUIDToAssetPath(normalGuids[0]);
            normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        }

        Texture2D shadowTex = null;
        if (shadowGuids.Length > 0)
        {
            string shadowPath = AssetDatabase.GUIDToAssetPath(shadowGuids[0]);
            shadowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(shadowPath);
        }

        Texture2D translucencyTex = null;
        if (translucencyGuids.Length > 0)
        {
            string translucencyPath = AssetDatabase.GUIDToAssetPath(translucencyGuids[0]);
            translucencyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(translucencyPath);
        }

        // Создаем материалы для URP
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            urpLitShader = Shader.Find("Shader Graphs/Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                urpLitShader = Shader.Find("URP/Lit");
            }
        }

        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("Ошибка", 
                "Не найден URP Lit шейдер! Убедитесь, что Universal Render Pipeline установлен.", 
                "OK");
            return;
        }

        // Создаем материал для коры
        Material barkMaterial = new Material(urpLitShader);
        barkMaterial.name = "TreeBarkMaterial_URP";
        if (diffuseTex != null)
        {
            barkMaterial.SetTexture("_BaseMap", diffuseTex);
        }
        if (normalTex != null)
        {
            barkMaterial.SetTexture("_BumpMap", normalTex);
            barkMaterial.EnableKeyword("_NORMALMAP");
            barkMaterial.SetFloat("_BumpScale", 1.0f);
        }
        barkMaterial.SetFloat("_Smoothness", 0.3f);
        barkMaterial.SetFloat("_Metallic", 0.0f);

        // Создаем материал для листьев
        Material leafMaterial = new Material(urpLitShader);
        leafMaterial.name = "TreeLeafMaterial_URP";
        if (diffuseTex != null)
        {
            leafMaterial.SetTexture("_BaseMap", diffuseTex);
        }
        if (normalTex != null)
        {
            leafMaterial.SetTexture("_BumpMap", normalTex);
            leafMaterial.EnableKeyword("_NORMALMAP");
            leafMaterial.SetFloat("_BumpScale", 1.0f);
        }
        leafMaterial.SetFloat("_Smoothness", 0.2f);
        leafMaterial.SetFloat("_Metallic", 0.0f);
        leafMaterial.SetFloat("_Cutoff", 0.3f);
        leafMaterial.EnableKeyword("_ALPHATEST_ON");

        // Сохраняем материалы
        string barkMaterialPath = "Assets/Materials/TreeBarkMaterial_URP.mat";
        string leafMaterialPath = "Assets/Materials/TreeLeafMaterial_URP.mat";
        
        AssetDatabase.CreateAsset(barkMaterial, barkMaterialPath);
        AssetDatabase.CreateAsset(leafMaterial, leafMaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Загружаем сохраненные материалы
        Material savedBarkMaterial = AssetDatabase.LoadAssetAtPath<Material>(barkMaterialPath);
        Material savedLeafMaterial = AssetDatabase.LoadAssetAtPath<Material>(leafMaterialPath);

        // Открываем префаб для редактирования
        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(treePrefabPath);
        MeshRenderer renderer = prefabInstance.GetComponent<MeshRenderer>();
        
        if (renderer != null)
        {
            // Применяем материалы
            Material[] materials = new Material[2];
            materials[0] = savedBarkMaterial;
            materials[1] = savedLeafMaterial;
            renderer.materials = materials;
            
            PrefabUtility.SaveAsPrefabAsset(prefabInstance, treePrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabInstance);
            
            EditorUtility.DisplayDialog("Успех", 
                "Материалы для дерева созданы и применены!\n\n" +
                "Bark Material: " + barkMaterialPath + "\n" +
                "Leaf Material: " + leafMaterialPath + "\n\n" +
                "Примечание: Unity Tree система не работает с URP. " +
                "Если дерево все еще розовое, возможно нужно использовать обычные GameObjects вместо Unity Tree компонента.",
                "OK");
        }
        else
        {
            PrefabUtility.UnloadPrefabContents(prefabInstance);
            EditorUtility.DisplayDialog("Предупреждение", 
                "Не найден MeshRenderer в префабе Tree. Возможно, используется Unity Tree компонент, который несовместим с URP.",
                "OK");
        }
    }
}
#endif
