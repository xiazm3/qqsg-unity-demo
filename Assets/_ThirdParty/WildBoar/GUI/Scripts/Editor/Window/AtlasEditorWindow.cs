using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace WildBoar.GUIModule
{
    internal class AtlasEditorWindow:EditorWindow
    {
        private SpriteAtlas spriteAtlas;

        private AtlasDrawer atlasDrawer;


        [MenuItem("Tools/Windows/AtlasEditorWindow")]
        static void Init()
        {
            var window = GetWindow<AtlasEditorWindow>();
            window.Show();
        }

        public void OnGUI()
        {

            if(GUILayout.Button("New Atlas"))
            {
                var path = EditorUtility.SaveFilePanel("file", "", "new atlas", "asset");
                if(path != null)
                {
                    SpriteAtlas spriteAtlas = ScriptableObject.CreateInstance<SpriteAtlas>();
                    var shader = Shader.Find("Unlit/Transparent Colored");
                    var material = new Material(shader);
                    var texture = new Texture2D(1, 1,TextureFormat.RGBA32,false);
                    texture.name = "Texture";
                    texture.alphaIsTransparency = true;
                    texture.filterMode = FilterMode.Point;

                    spriteAtlas.Texture = texture;
                    spriteAtlas.Material= material;
                    material.SetTexture("_MainTex", texture);

                    var atlasPath =Path.GetRelativePath(Application.dataPath,path);
                    atlasPath = Path.Combine("Assets", atlasPath);
                    AssetDatabase.CreateAsset(spriteAtlas, atlasPath);

                    AssetDatabase.AddObjectToAsset(texture, atlasPath);
                    AssetDatabase.AddObjectToAsset(material, atlasPath);
                    AssetDatabase.SaveAssets();
                }

            }


            var newAtlas = EditorGUILayout.ObjectField(spriteAtlas,typeof(SpriteAtlas),false) as SpriteAtlas;
            if(newAtlas != spriteAtlas)
            {
                SetAtlas(newAtlas);
            }

            if (spriteAtlas != null)
            {
                if (GUILayout.Button("一键导出全部图片为UGUI Sprite"))
                {
                    ExportAllToUGUISprites();
                }
            }



            if(atlasDrawer !=null)
            {
                atlasDrawer.DrawSpriteDatas();
            }
            
        }

        private void SetAtlas(SpriteAtlas atlas)
        {
            spriteAtlas= atlas;
            atlasDrawer = new AtlasDrawer(atlas, null);
            atlasDrawer.OnSpriteRightClick = ShowRightClickMenu;
            atlasDrawer.OnAcceptTextures = OnAddTexture;
        }

        private void OnAddTexture(List<Texture2D> textureList)
        {
            var spriteDataList = new List<SpriteData>();
            foreach(var texture in textureList)
            {

                var rwTexture = new Texture2D(texture.width,texture.height, texture.format, false, true);

                Graphics.CopyTexture(texture, rwTexture);

                var spriteData = new SpriteData()
                {
                    Name = texture.name,
                    Rect = new Rect(0, 0, texture.width, texture.height),
                    Texture = rwTexture,
                };

                spriteDataList.Add(spriteData);

            }

            spriteAtlas.AddSpritesAndRebuild(spriteDataList);
            spriteAtlas.SetDirty();
            AssetDatabase.SaveAssets();
            SetAtlas(spriteAtlas);
        }


        private void ShowRightClickMenu(SpriteData spriteData)
        {
            NGUIContextMenu.AddItem("Remove", false, RemoveSprite, spriteData);
            NGUIContextMenu.Show();
        }

        void RemoveSprite(object obj)
        {
            if (this == null) return;
            var spriteData = obj as SpriteData;
            spriteAtlas.Remove(spriteData);
            EditorUtility.SetDirty(spriteAtlas);
            AssetDatabase.SaveAssets();
            SetAtlas(spriteAtlas);
        }

        private void ExportAllToUGUISprites()
        {
            var folder = EditorUtility.OpenFolderPanel("选择导出文件夹", Application.dataPath, "UGUISprites");
            if (string.IsNullOrEmpty(folder)) return;
            var inProject = folder.Replace("\\", "/").StartsWith(Application.dataPath.Replace("\\", "/"));
            if (!inProject)
            {
                folder = Path.Combine(Application.dataPath, "UGUISprites");
                Directory.CreateDirectory(folder);
            }
            var list = spriteAtlas.GetSpriteDataWithStandaloneTextureList();
            foreach (var data in list)
            {
                var tex = data.Texture;
                if (tex == null) continue;
                tex.Apply();
                var bytes = tex.EncodeToPNG();
                var fileName = data.Name;
                if (string.IsNullOrEmpty(fileName)) fileName = "sprite";
                var absPath = Path.Combine(folder, fileName + ".png");
                var dir = Path.GetDirectoryName(absPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(absPath, bytes);
                var relPath = Path.Combine("Assets", Path.GetRelativePath(Application.dataPath, absPath));
                relPath = relPath.Replace("\\", "/");
                AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(relPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100f;
                    importer.spritePivot = data.Pivot;
                    importer.spriteBorder = data.Slice9Padding;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Point;
                    AssetDatabase.WriteImportSettingsIfDirty(relPath);
                    AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);
                }
            }
            AssetDatabase.Refresh();
        }
    }
}
