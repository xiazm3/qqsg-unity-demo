using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;


public class PlayerInputMove : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 3f;
    [SerializeField]
    private float climbSpeed = 1f;

    [SerializeField]
    private float jumpSpeed = 5f;
    [SerializeField]
    private float climbAttachThreshold = 0.3f;

    public bool onPlatform => platformRigidbody.onPlatform;

    private PlatformRigidBody platformRigidbody;
    private GravityRigidBody gravityRigidbody;
    private SceneUnitAvatarHolder avatarHolder;


    public float inputX { get; private set; }
    public float inputY { get; private set; }

    public Vector2 InputSpeed => Vector2.right * moveSpeed * inputX + Vector2.up * inputY * climbSpeed;

    private Platform climbPlatform;
    private Vector3 needClimbPos;

    private bool autoMoveActive;
    private float autoMoveX;

    public bool IsAutoMoveActive => autoMoveActive;

    void Start()
    {
        platformRigidbody= GetComponent<PlatformRigidBody>();
        gravityRigidbody = GetComponent<GravityRigidBody>();
        avatarHolder = GetComponent<SceneUnitAvatarHolder>();

        DontDestroyOnLoad(this.gameObject);
    }


    // Update is called once per frame
    void Update()
    {
        inputX = 0;
        inputY = 0;

        if (InputManager.Instance.GetInput(InputEnum.MoveLeft))
            inputX = -1;
        if (InputManager.Instance.GetInput(InputEnum.MoveRight))
            inputX = 1;
        if (InputManager.Instance.GetInput(InputEnum.MoveUp))
            inputY = 1;
        if (InputManager.Instance.GetInput(InputEnum.MoveDown))
            inputY = -1;

        if (autoMoveActive)
        {
            if (Mathf.Abs(inputX) > 0.01f)
            {
                StopAutoMoveX();
            }
            else
            {
                inputX = autoMoveX;
            }
        }

        if(PlayerSingleton.Instance.State.CanInput())
        {
            HandleRun();
            HandleJump();
            HandleClimb();
        }


        ResetInputWhenBlock();
    }

    private void HandleRun()
    {
        FlipSprite();
        ChangeAnimation();
    }

    private void ResetInputWhenBlock()
    {
        foreach(var blockCollider in BlockCollider.List)
        {
            if (blockCollider.BoxCollider == null)
                continue;

            if(blockCollider.BoxCollider.bounds.Contains(transform.position))
            {
                if(gravityRigidbody.velocity.x * blockCollider.ForbiddenVector().x > 0)
                    gravityRigidbody.velocity.x = 0;

                if (inputX * blockCollider.ForbiddenVector().x > 0)
                    inputX = 0;

            }
        }

    }


    private void FlipSprite()
    {
        if (!onPlatform)
            return;
        if(inputX > 0.01f)
        {
            this.transform.localScale = new Vector3(1, 1,1);
        }
        else if(inputX < -0.01f)
        {
            this.transform.localScale = new Vector3(-1, 1,1);
        }
    }
    private void ChangeAnimation()
    {
        if(onPlatform)
        {
            if(platformRigidbody.PlatformDirect == Platform.Direction.Horizontal)
            {
                if (inputX == 0)
                {
                    if(PlayerSingleton.Instance.State.IsFighting)
                        avatarHolder.Play(AvatarAnimEnum.BattleIdle01);
                    else
                        avatarHolder.Play(AvatarAnimEnum.Idle00);
                }
                else if (Mathf.Abs(inputX) > 0.01f)
                {
                    avatarHolder.Play(AvatarAnimEnum.Run03);
                }
            }
            else if (platformRigidbody.PlatformDirect == Platform.Direction.Vertical)
            {
                avatarHolder.Play(AvatarAnimEnum.Climb06);
                if (inputY == 0)
                    avatarHolder.Pause();
                else
                    avatarHolder.Resume();

            }

        }
        else
        {
            avatarHolder.Play(AvatarAnimEnum.Jump05);
        }
    }

    private void HandleJump()
    {
        if(InputManager.Instance.GetInputDown(InputEnum.Jump))
        {
            platformRigidbody.Jump(jumpSpeed);
            avatarHolder.Play(AvatarAnimEnum.Jump05);
        }
    }


    private void HandleClimb()
    {
        bool wantClimbUpOnPlatform = (inputY > 0) && onPlatform;
        bool wantClimbDownOnPlatform = (inputY < 0) && onPlatform && gravityRigidbody.CheckVerticalPlatform(Vector2.down) != null;
        bool needClimbWhenJump = (inputY != 0);

        bool needClimb = needClimbWhenJump || wantClimbUpOnPlatform || wantClimbDownOnPlatform;

        if (!needClimb)
        {
            climbPlatform = null;

            return;
        }

        //need climb


        if (climbPlatform == null)
        {
            //Try find climbPlaytform
            Collider2D collider = null;
            if (onPlatform)
            {
                collider = gravityRigidbody.CheckVerticalPlatform(Vector2.up * Mathf.Sign(inputY));
                if (collider == null)
                {
                    var left = gravityRigidbody.CheckVerticalPlatform(Vector2.left);
                    var right = gravityRigidbody.CheckVerticalPlatform(Vector2.right);
                    collider = left != null ? left : right;
                }
                if (collider == null)
                {
                    var list = Physics2D.OverlapCircleAll(transform.position, climbAttachThreshold, LayerMask.GetMask("ClimbPlatform"));
                    float nearestDist = float.MaxValue;
                    Collider2D nearestCol = null;
                    foreach (var c in list)
                    {
                        var p = c.GetComponent<Platform>();
                        if (p == null)
                            continue;
                        var nearest = p.NearestPointOnLine(this.transform.position);
                        var dist = (nearest - this.transform.position).magnitude;
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestCol = c;
                        }
                    }
                    collider = nearestCol;
                }
            }
            else
            {
                var left = gravityRigidbody.CheckVerticalPlatform(Vector2.left);
                var right = gravityRigidbody.CheckVerticalPlatform(Vector2.right);
                collider = left != null ? left : right;
                if (collider == null)
                    collider = gravityRigidbody.CheckVerticalPlatform(inputY > 0 ? Vector2.up : Vector2.down);
                if (collider == null)
                {
                    var list = Physics2D.OverlapCircleAll(transform.position, climbAttachThreshold, LayerMask.GetMask("ClimbPlatform"));
                    float nearestDist = float.MaxValue;
                    Collider2D nearestCol = null;
                    foreach (var c in list)
                    {
                        var p = c.GetComponent<Platform>();
                        if (p == null)
                            continue;
                        var nearest = p.NearestPointOnLine(this.transform.position);
                        var dist = (nearest - this.transform.position).magnitude;
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestCol = c;
                        }
                    }
                    collider = nearestCol;
                }
            }
            if(collider != null)
            {
                climbPlatform = collider.GetComponent<Platform>();
                needClimbPos = this.transform.position;

            }
        }


        if (climbPlatform != null)
        {
            var topY = Mathf.Max(climbPlatform.StartPoint.y, climbPlatform.EndPoint.y);
            if (onPlatform && platformRigidbody.PlatformDirect == Platform.Direction.Horizontal && transform.position.y >= topY - climbAttachThreshold && inputY > 0)
            {
                climbPlatform = null;
                return;
            }
            var bottomY = Mathf.Min(climbPlatform.StartPoint.y, climbPlatform.EndPoint.y);
            if (onPlatform && platformRigidbody.PlatformDirect == Platform.Direction.Horizontal && transform.position.y <= bottomY + climbAttachThreshold && inputY < 0)
            {
                climbPlatform = null;
                return;
            }
            var platformX = climbPlatform.transform.position.x;
            var xDiffNeed = needClimbPos.x - platformX;
            var xDiff = transform.position.x - platformX;
            var nearest = climbPlatform.NearestPointOnLine(this.transform.position);
            var perpendicularDistance = (nearest - this.transform.position).magnitude;

            if (xDiff * xDiffNeed <=0 || perpendicularDistance <= climbAttachThreshold)
            {
                this.transform.position = nearest;
                platformRigidbody.LandPlatformFromJump(climbPlatform);
            }

        }

    }

    public void SetAutoMoveX(float direction)
    {
        autoMoveActive = true;
        autoMoveX = Mathf.Clamp(direction, -1f, 1f);
    }

    public void StopAutoMoveX()
    {
        autoMoveActive = false;
        autoMoveX = 0;
    }



}
