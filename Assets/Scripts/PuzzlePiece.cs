using System.Collections;
using UnityEngine;

public class PuzzlePiece
{
    public Transform Transform;         // Link to transform
    public Vector3 StartPosition;       // Initial position	
    public Quaternion StartRotation;    // Initial rotation
    public Vector3 TargetPosition;      // Target position for movement
    public Renderer Renderer;           // Link to renderer
    public Vector3 Size;                // Size of piece

    private bool useLocalSpace;
    private float movementTime;
    private Vector3 velocity = Vector3.zero;
    private Material materialAssembled;  // Material when piece assembled in puzzle

    public PuzzlePiece(Transform transform, Material _materialAssembled)
    {
        Transform = transform;
        StartPosition = transform.localPosition;
        StartRotation = transform.localRotation;

        Renderer = transform.GetComponent<Renderer>();
        materialAssembled = _materialAssembled;

        Size = Renderer.bounds.size;
    }

    // Calculate piece rendedrer center offset from the piece pivot
    public Vector2 GetPieceCenterOffset()
    {
        Vector2 pieceCenterOffset = new Vector2(Renderer.bounds.center.x - Transform.position.x, Renderer.bounds.center.y - Transform.position.y);

        return pieceCenterOffset;
    }

    // Process piece movement
    public IEnumerator Move(Vector3 targetPosition, bool _inLocalSpace, float _movementTime)
    {
        // Initialize
        TargetPosition = targetPosition;
        useLocalSpace = _inLocalSpace;
        movementTime = _movementTime;

        // Use proper positions data according to used movement space (Local or World) and Smoothly move piece until it reaches targetPosition
        while (Vector3.Distance(useLocalSpace ? Transform.localPosition : Transform.position, TargetPosition) > 0.1f)
        {
            if (useLocalSpace)
                Transform.localPosition = Vector3.SmoothDamp(Transform.localPosition, TargetPosition, ref velocity, movementTime);
            else
                Transform.position = Vector3.SmoothDamp(Transform.position, TargetPosition, ref velocity, movementTime);

            yield return null;
        }

        // Set final position and Assemble if needed
        if (useLocalSpace)
            Transform.localPosition = TargetPosition;
        else
            Transform.position = TargetPosition;

        if (TargetPosition == StartPosition)
            Assemble();
    }

    // Set to assembled state
    public void Assemble()
    {
        if (Transform.childCount > 0)
            Transform.GetChild(0).gameObject.SetActive(false);

        Renderer.material = materialAssembled;
    }
}