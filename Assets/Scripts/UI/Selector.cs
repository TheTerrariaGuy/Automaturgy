using Assets.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;


public class Selector : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    private Vector3 spriteSize;
    private float t;
    private Vector3 initPos;

    
    void Start()
    {
        initPos = transform.localPosition;
        spriteSize = spriteRenderer.sprite.bounds.size;
        // Keep selection artwork scaling separate from the animated selector root.
        var artwork = new GameObject("Selection artwork");
        artwork.layer = spriteRenderer.gameObject.layer;
        artwork.transform.SetParent(spriteRenderer.transform, false);
        artwork.transform.localPosition = spriteRenderer.sprite.bounds.center;
        var preview = artwork.AddComponent<SpriteRenderer>();
        preview.sharedMaterial = spriteRenderer.sharedMaterial;
        preview.sortingLayerID = spriteRenderer.sortingLayerID;
        preview.sortingOrder = spriteRenderer.sortingOrder;
        spriteRenderer.enabled = false;
        spriteRenderer = preview;
        GameLogic.INSTANCE.SelectionChanged += ShowSelection;
        ShowSelection(GameLogic.INSTANCE.CurrentSelection);
    }
    
    private void ShowSelection(int type)
    {
        spriteRenderer.enabled = type != 0;
        if (type == 0) return;
        Sprite sprite = TextureHandler.INSTANCE.GetPreviewSprite(type);
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        if (sprite != null)
        {
            spriteRenderer.transform.localScale = new Vector3(
                spriteSize.x / sprite.bounds.size.x, spriteSize.y / sprite.bounds.size.y, 1f);
        }
    }

    void Update()
    {
        HandleAnimations();
        
        if (Keyboard.current == null) return;
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            GameLogic.INSTANCE.SubmitQueuedSpells();
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            GameLogic.INSTANCE.UpdateSelection(100);
        }
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            GameLogic.INSTANCE.UpdateSelection(200);
        }
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            GameLogic.INSTANCE.UpdateSelection(300);
        }
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            GameLogic.INSTANCE.UpdateSelection(400);
        }
    }
    private void HandleAnimations()
    {
        t += Time.deltaTime;

        if (t > 1f)
        {
            t -= 1f;
        }

        transform.localPosition = initPos + 0.15f * Vector3.up * Mathf.Pow(Mathf.Sin((t * 2f - 0.5f) * Mathf.PI), 2);

    }
    private void OnDestroy()
    {
        if (GameLogic.INSTANCE != null) GameLogic.INSTANCE.SelectionChanged -= ShowSelection;
    }
}
