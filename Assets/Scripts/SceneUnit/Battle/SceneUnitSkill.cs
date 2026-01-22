using Assets.Scripts.ScriptableObj;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SceneUnitSkill:MonoBehaviour
{
    public SceneUnitInfo CurrentSkillTarget { get; private set; }
    public Skill_ScriptableObj CurrentSkill { get; private set; }
    public SubSkill_ScriptableObj CurrentSubSkill { get; private set; }

    [SerializeField]
    private float autoChaseStopDistance = 0.6f;

    public SceneUnitInfo SceneUnit { get; private set; }
    public SceneUnitEffectManager EffectManager { get; private set; }
    public SceneUnitAvatarHolder AvatarHolder { get; private set; }
    public PlayerInputMove PlayerInputMove { get; private set; }

    private bool autoChaseCanceled;

    public void Start()
    {
        var hasSceneUnitInfo = GetComponent<IHasSceneUnitInfo>();
        SceneUnit = hasSceneUnitInfo.GetSceneUnit();
        EffectManager = GetComponent<SceneUnitEffectManager>();
        AvatarHolder = GetComponent<SceneUnitAvatarHolder>();
        PlayerInputMove = GetComponent<PlayerInputMove>();
        AvatarHolder.OnTriggerAnimeEvent += HandleEvent;

    }

    public void UseSkill(Skill_ScriptableObj skill)
    {
        CurrentSkill= skill;
        TryAutoSelectTarget();
        CurrentSkillTarget = SceneUnit.Target;
        StartCoroutine(UseSkillInner());
    }

    private IEnumerator UseSkillInner()
    {
        autoChaseCanceled = false;
        if (CurrentSkillTarget != null)
        {
            yield return AutoChaseToTarget();
        }

        if (autoChaseCanceled)
        {
            CurrentSkill = null;
            yield break;
        }

        foreach(var subSkill in CurrentSkill.SubSkillList)
        {
            UseSubSkill(subSkill);
            yield return new WaitForSeconds(subSkill.Duration);
        }

        CurrentSkill = null;
    }

    public bool TryAutoSelectTarget()
    {
        if (SceneUnit == null)
            return false;
        if (SceneUnit.Target != null)
            return true;

        var nearestOwner = FindNearestEnemyOwner();
        if (nearestOwner == null)
            return false;

        MouseManager.SetFocusedSceneUnit(nearestOwner);
        return SceneUnit.Target != null;
    }

    private IHasSceneUnitInfo FindNearestEnemyOwner()
    {
        var monsters = GameObject.FindObjectsOfType<MonsterBoxColliderManager>();
        if (monsters == null || monsters.Length == 0)
            return null;

        var selfPos = SceneUnit?.SelfGameObj != null ? SceneUnit.SelfGameObj.transform.position : transform.position;
        MonsterBoxColliderManager nearest = null;
        var minDistance = float.MaxValue;
        foreach (var monster in monsters)
        {
            if (monster == null || monster.monsterUnit == null)
                continue;

            var info = monster.GetSceneUnit();
            if (info == null || info.HP <= 0 || info.SelfGameObj == null)
                continue;

            var diff = info.SelfGameObj.transform.position - selfPos;
            var distance = diff.sqrMagnitude;
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = monster;
            }
        }

        return nearest;
    }

    private IEnumerator AutoChaseToTarget()
    {
        var target = CurrentSkillTarget;
        if (target == null || target.SelfGameObj == null || PlayerInputMove == null)
            yield break;

        while (CurrentSkillTarget == target)
        {
            if (target.HP <= 0 || target.SelfGameObj == null)
            {
                PlayerInputMove.StopAutoMoveX();
                yield break;
            }

            if (InputManager.Instance.GetInput(InputEnum.MoveLeft) || InputManager.Instance.GetInput(InputEnum.MoveRight))
            {
                PlayerInputMove.StopAutoMoveX();
                autoChaseCanceled = true;
                yield break;
            }

            var targetPos = target.SelfGameObj.transform.position;
            var selfPos = transform.position;
            var distance = Mathf.Abs(targetPos.x - selfPos.x);
            if (distance <= autoChaseStopDistance)
            {
                PlayerInputMove.StopAutoMoveX();
                yield break;
            }

            var dir = targetPos.x > selfPos.x ? 1f : -1f;
            PlayerInputMove.SetAutoMoveX(dir);

            yield return null;
        }

        PlayerInputMove.StopAutoMoveX();
    }

    private void UseSubSkill(SubSkill_ScriptableObj subSkill)
    {
        CurrentSubSkill = subSkill;
        foreach (var selfEffect in subSkill.SelfEffectList)
        {
            AddEffectToManager(selfEffect);
        }

        if (!string.IsNullOrEmpty(subSkill.SoundId))
            SoundManager.Instance.PlayEffect(subSkill.SoundId);

        if(subSkill.Damage > 0)
        {
            SoundManager.Instance.PlayEffect(CurrentSkillTarget.HitSoundID);

            CurrentSkillTarget.DecreaseHP(subSkill.Damage);
        }

        AvatarHolder.Play(subSkill.ActionAnimation, true);
    }

    public void HandleEvent(AnimEventEnum animEvent)
    {
        foreach(var eventEffect in CurrentSubSkill.EffectOnEventList)
        {
            if(eventEffect.Event == animEvent)
            {
                AddEffectToManager(eventEffect.Effect);
            }

        }

    }


    private void AddEffectToManager(BaseEffect baseEffect)
    {
        var instanceEffect = Instantiate(baseEffect);
        instanceEffect.CasterSceneUnit = SceneUnit;
        instanceEffect.TargetSceneUnit = CurrentSkillTarget;
        EffectManager.AddEffect(instanceEffect);
    }
}
