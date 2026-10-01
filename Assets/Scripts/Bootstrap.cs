using System;
using Assets.Scripts.Inventory;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts
{
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        public TextAsset spellSource;
        private bool failed;

        private void Start()
        {
            try
            {
                ReactionCatalog.Initialize(spellSource);
                SceneManager.LoadSceneAsync(InventorySession.InventoryScene);
            }
            catch (Exception error)
            {
                failed = true;
                Debug.LogException(error, this);
            }
        }

        private void OnGUI()
        {
            if (failed) GUI.Label(new Rect(24, 24, 600, 80), "Spells could not be loaded. Check the spell definitions.");
        }
    }
}
