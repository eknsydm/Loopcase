using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float knockbackDecay = 60f;

    [Tooltip("Children that must not mirror when the character turns, such as a name label or health bar. Left empty, every child canvas is kept upright.")]
    [SerializeField] private Transform[] uprightChildren;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 knockbackVelocity;

    private Animator animator;
    private bool facingRight = true;

    private Vector3[] uprightScales;
    private Vector3[] uprightPositions;
    private float appliedFacingSign = 1f;
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        CacheUprightChildren();
    }

    private void CacheUprightChildren()
    {
        if (uprightChildren == null || uprightChildren.Length == 0)
        {

            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            uprightChildren = new Transform[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                uprightChildren[i] = canvases[i].transform;
            }
        }

  
        uprightScales = new Vector3[uprightChildren.Length];
        uprightPositions = new Vector3[uprightChildren.Length];
        for (int i = 0; i < uprightChildren.Length; i++)
        {
            if (uprightChildren[i] == null) continue;

            uprightScales[i] = uprightChildren[i].localScale;
            uprightPositions[i] = uprightChildren[i].localPosition;
        }
    }

    void Update()
    {

        if (knockbackVelocity != Vector2.zero)
        {
            rb.linearVelocity = knockbackVelocity;
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, knockbackDecay * Time.deltaTime);
        }
        else
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }

        UpdateFacing();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        knockbackVelocity = direction.normalized * force;
    }

    private void UpdateFacing()
    {
        if (moveInput.x > 0.01f) facingRight = true;
        else if (moveInput.x < -0.01f) facingRight = false;


        Vector3 scale = transform.localScale;
        scale.x = facingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        transform.localScale = scale;

        KeepChildrenUpright();
    }

  
    private void KeepChildrenUpright()
    {
        float sign = facingRight ? 1f : -1f;
        if (Mathf.Approximately(sign, appliedFacingSign)) return;

        appliedFacingSign = sign;
        for (int i = 0; i < uprightChildren.Length; i++)
        {
            Transform child = uprightChildren[i];
            if (child == null) continue;

            Vector3 scale = uprightScales[i];
            Vector3 position = uprightPositions[i];
            child.localScale = new Vector3(scale.x * sign, scale.y, scale.z);
            child.localPosition = new Vector3(position.x * sign, position.y, position.z);
        }
    }


    public void SetMoveInput(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        bool stopping = input.sqrMagnitude < 0.0001f;

        if (animator != null)
        {

            if (stopping && moveInput.sqrMagnitude > 0.0001f)
            {
                animator.SetFloat("LastInputX", moveInput.x);
                animator.SetFloat("LastInputY", moveInput.y);
            }

            animator.SetBool("isWalking", !stopping);
            animator.SetFloat("InputX", input.x);
            animator.SetFloat("InputY", input.y);
        }

        moveInput = input;
    }

    public void Move(InputAction.CallbackContext context)
    {
        animator.SetBool("isWalking",true);

        if (context.canceled)
        {
            animator.SetBool("isWalking",false);
            animator.SetFloat("LastInputX",moveInput.x);
            animator.SetFloat("LastInputY",moveInput.y);

        }
        moveInput = context.ReadValue<Vector2>();
        animator.SetFloat("InputX",moveInput.x);
        animator.SetFloat("InputY",moveInput.y);

    }
}
