using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShootBallJoystick))]
public class ShootBallJoystickEditor : JoystickEditor
{
    private SerializedProperty maxPower;
    private SerializedProperty spinFactor;
    private SerializedProperty accuracyLossStartPower01;
    private SerializedProperty wildShotStartPower01;
    private SerializedProperty normalMaxInaccuracyAngle;
    private SerializedProperty wildMaxInaccuracyAngle;
    private SerializedProperty autoShootDelayAfterFullPower;
    private SerializedProperty buttonBody;
    private SerializedProperty shadowImage;
    private SerializedProperty glowImage;
    private SerializedProperty marbleVisual;
    private SerializedProperty pressedScale;
    private SerializedProperty pressedOffsetY;
    private SerializedProperty pressDownDuration;
    private SerializedProperty releaseDuration;
    private SerializedProperty pressedShadowAlpha;
    private SerializedProperty heldGlowAlpha;
    private SerializedProperty heldGlowRiseDuration;
    private SerializedProperty marbleSpinDuration;
    private SerializedProperty straightShotIcon;
    private SerializedProperty backShotIcon;
    private SerializedProperty directionIconDeadZone;
    private SerializedProperty hideDirectionIconsOnRelease;
    private SerializedProperty idleIconAlpha;
    private SerializedProperty idleIconScale;
    private SerializedProperty activeIconScale;
    private SerializedProperty iconAppearDuration;
    private SerializedProperty iconAppearStagger;
    private SerializedProperty iconHighlightDuration;
    private SerializedProperty activeIconTint;

    protected override void OnEnable()
    {
        base.OnEnable();
        maxPower = serializedObject.FindProperty("maxPower");
        spinFactor = serializedObject.FindProperty("spinFactor");
        accuracyLossStartPower01 = serializedObject.FindProperty("accuracyLossStartPower01");
        wildShotStartPower01 = serializedObject.FindProperty("wildShotStartPower01");
        normalMaxInaccuracyAngle = serializedObject.FindProperty("normalMaxInaccuracyAngle");
        wildMaxInaccuracyAngle = serializedObject.FindProperty("wildMaxInaccuracyAngle");
        autoShootDelayAfterFullPower = serializedObject.FindProperty("autoShootDelayAfterFullPower");
        buttonBody = serializedObject.FindProperty("buttonBody");
        shadowImage = serializedObject.FindProperty("shadowImage");
        glowImage = serializedObject.FindProperty("glowImage");
        marbleVisual = serializedObject.FindProperty("marbleVisual");
        pressedScale = serializedObject.FindProperty("pressedScale");
        pressedOffsetY = serializedObject.FindProperty("pressedOffsetY");
        pressDownDuration = serializedObject.FindProperty("pressDownDuration");
        releaseDuration = serializedObject.FindProperty("releaseDuration");
        pressedShadowAlpha = serializedObject.FindProperty("pressedShadowAlpha");
        heldGlowAlpha = serializedObject.FindProperty("heldGlowAlpha");
        heldGlowRiseDuration = serializedObject.FindProperty("heldGlowRiseDuration");
        marbleSpinDuration = serializedObject.FindProperty("marbleSpinDuration");
        straightShotIcon = serializedObject.FindProperty("straightShotIcon");
        backShotIcon = serializedObject.FindProperty("backShotIcon");
        directionIconDeadZone = serializedObject.FindProperty("directionIconDeadZone");
        hideDirectionIconsOnRelease = serializedObject.FindProperty("hideDirectionIconsOnRelease");
        idleIconAlpha = serializedObject.FindProperty("idleIconAlpha");
        idleIconScale = serializedObject.FindProperty("idleIconScale");
        activeIconScale = serializedObject.FindProperty("activeIconScale");
        iconAppearDuration = serializedObject.FindProperty("iconAppearDuration");
        iconAppearStagger = serializedObject.FindProperty("iconAppearStagger");
        iconHighlightDuration = serializedObject.FindProperty("iconHighlightDuration");
        activeIconTint = serializedObject.FindProperty("activeIconTint");
    }

    protected override void DrawValues()
    {
        base.DrawValues();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shoot Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxPower);
        EditorGUILayout.PropertyField(spinFactor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shot Accuracy", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(accuracyLossStartPower01);
        EditorGUILayout.PropertyField(wildShotStartPower01);
        EditorGUILayout.PropertyField(normalMaxInaccuracyAngle);
        EditorGUILayout.PropertyField(wildMaxInaccuracyAngle);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto Shoot", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(autoShootDelayAfterFullPower);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Press Feedback", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(buttonBody);
        EditorGUILayout.PropertyField(shadowImage);
        EditorGUILayout.PropertyField(glowImage);
        EditorGUILayout.PropertyField(marbleVisual);
        EditorGUILayout.PropertyField(pressedScale);
        EditorGUILayout.PropertyField(pressedOffsetY);
        EditorGUILayout.PropertyField(pressDownDuration);
        EditorGUILayout.PropertyField(releaseDuration);
        EditorGUILayout.PropertyField(pressedShadowAlpha);
        EditorGUILayout.PropertyField(heldGlowAlpha);
        EditorGUILayout.PropertyField(heldGlowRiseDuration);
        EditorGUILayout.PropertyField(marbleSpinDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Forward / Back Spin Icons", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(straightShotIcon);
        EditorGUILayout.PropertyField(backShotIcon);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spin Icon Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(directionIconDeadZone);
        EditorGUILayout.PropertyField(hideDirectionIconsOnRelease);
        EditorGUILayout.PropertyField(idleIconAlpha);
        EditorGUILayout.PropertyField(idleIconScale);
        EditorGUILayout.PropertyField(activeIconScale);
        EditorGUILayout.PropertyField(iconAppearDuration);
        EditorGUILayout.PropertyField(iconAppearStagger);
        EditorGUILayout.PropertyField(iconHighlightDuration);
        EditorGUILayout.PropertyField(activeIconTint);
    }
}
