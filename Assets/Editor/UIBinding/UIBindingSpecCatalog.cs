using System;
using UnityEngine;

namespace AttackSkill.Editor.UIBinding
{
    /// <summary>单个绑定字段：C# 字段名 ← Prefab 节点名。</summary>
    public readonly struct UIBindingField
    {
        public readonly string FieldName;
        public readonly Type ComponentType;
        public readonly string NodeName;
        public readonly bool CollectAll;

        public UIBindingField(string fieldName, Type componentType, string nodeName = null, bool collectAll = false)
        {
            FieldName = fieldName;
            ComponentType = componentType;
            NodeName = string.IsNullOrEmpty(nodeName) ? fieldName : nodeName;
            CollectAll = collectAll;
        }
    }

    public sealed class UIBindingViewSpec
    {
        public string ClassName;
        public string PrefabName;
        /// <summary>可选：相对 Assets 的完整 Prefab 路径（含或不含 .prefab）。优先于 PrefabFolder/PrefabName。</summary>
        public string PrefabPath;
        public UIBindingField[] Fields;
    }

    /// <summary>各 View 需要生成/同步的绑定表。</summary>
    public static class UIBindingSpecCatalog
    {
        public static readonly UIBindingViewSpec[] All =
        {
            new UIBindingViewSpec
            {
                ClassName = "UISettingDialog",
                PrefabName = "UI_Setting_Dialog",
                Fields = new[]
                {
                    new UIBindingField("srlSetting", typeof(UnityEngine.UI.ScrollRect)),
                    new UIBindingField("item", typeof(RectTransform), "Item"),
                    new UIBindingField("dropTextSelection", typeof(UnityEngine.UI.Dropdown)),
                    new UIBindingField("dropGenderSelection", typeof(UnityEngine.UI.Dropdown)),
                    new UIBindingField("btnSound", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnPortrait", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnLanguage", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnGender", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("palSound", typeof(RectTransform)),
                    new UIBindingField("palPortrait", typeof(RectTransform)),
                    new UIBindingField("palLanguage", typeof(RectTransform)),
                    new UIBindingField("palGender", typeof(RectTransform)),
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button), collectAll: true),
                    new UIBindingField("btnClose", typeof(UnityEngine.UI.Button), collectAll: true),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UILogInDialog",
                PrefabName = "UI_LogIn_Dialog",
                Fields = new[]
                {
                    new UIBindingField("inputAccount", typeof(UnityEngine.UI.InputField)),
                    new UIBindingField("inputPassword", typeof(UnityEngine.UI.InputField)),
                    new UIBindingField("btnSure", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnCancel", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIChooseGenderDialog",
                PrefabName = "UI_ChooseGender_Dialog",
                Fields = new[]
                {
                    new UIBindingField("dropGender", typeof(UnityEngine.UI.Dropdown)),
                    new UIBindingField("btnSure", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnCancel", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIOpenScene4Panel",
                PrefabName = "UI_OpenScene4_Panel",
                Fields = new[]
                {
                    new UIBindingField("btnSetting", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnTool", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnAgeRem", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnNotice", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnAccount", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnLink", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnFemale", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnMale", typeof(UnityEngine.UI.Button)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIChangeScenePanel",
                PrefabName = "UI_ChangeScene_Panel",
                Fields = new[]
                {
                    new UIBindingField("slrPro", typeof(UnityEngine.UI.Slider)),
                    new UIBindingField("txtPro", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtTip", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtTitle", typeof(UnityEngine.UI.Text)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIOpenScene3Panel",
                PrefabName = "UI_OpenScene3_Panel",
                Fields = new[]
                {
                    new UIBindingField("slrPro", typeof(UnityEngine.UI.Slider)),
                    new UIBindingField("txtPro", typeof(UnityEngine.UI.Text)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UICommonSureDialog",
                PrefabName = "UI_CommonSure_Dialog",
                Fields = new[]
                {
                    new UIBindingField("txtTitle", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtTip", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("btnSure", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnCancel", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UICommonTipDialog",
                PrefabName = "UI_CommonTip_Dialog",
                Fields = new[]
                {
                    new UIBindingField("txtTip", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("imgBg", typeof(UnityEngine.UI.Image)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIToolsDialog",
                PrefabName = "UI_Tools_Dialog",
                Fields = new[]
                {
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnRemovePatch", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnNetworkTest", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnLogUpload", typeof(UnityEngine.UI.Button)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIAgeRemDialog",
                PrefabName = "UI_AgeRem_Dialog",
                Fields = new[]
                {
                    new UIBindingField("scrollView", typeof(UnityEngine.UI.ScrollRect), "Scroll View"),
                    new UIBindingField("content", typeof(RectTransform), "Content"),
                    new UIBindingField("item", typeof(RectTransform), "Item"),
                    new UIBindingField("txtTip", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("viewport", typeof(RectTransform), "Viewport"),
                    new UIBindingField("btnExit", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnSure", typeof(UnityEngine.UI.Button)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIBattleSystemPanel",
                PrefabName = "UI_BattleSystem_Panel",
                PrefabPath = "Assets/Prefabs/UI/BattleHUD/UI_BattleSystem_Panel.prefab",
                Fields = new[]
                {
                    new UIBindingField("palEntries", typeof(RectTransform)),
                    new UIBindingField("btnEntry_1", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnEntry_2", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnEntry_3", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnEntry_4", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnEntry_5", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnEntry_6", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("txtLatency", typeof(UnityEngine.UI.Text)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIBattlePartyPanel",
                PrefabName = "UI_BattleParty_Panel",
                PrefabPath = "Assets/Prefabs/UI/BattleHUD/UI_BattleParty_Panel.prefab",
                Fields = new[]
                {
                    new UIBindingField("palSlots", typeof(RectTransform)),
                    new UIBindingField("palAvatar1", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("palAvatar2", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("palAvatar3", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("imgAvatar", typeof(UnityEngine.UI.Image), collectAll: true),
                    new UIBindingField("imgKeyBadge", typeof(UnityEngine.UI.Image), collectAll: true),
                    new UIBindingField("txtKey", typeof(UnityEngine.UI.Text), collectAll: true),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIBattleVitalsPanel",
                PrefabName = "UI_BattleVitals_Panel",
                PrefabPath = "Assets/Prefabs/UI/BattleHUD/UI_BattleVitals_Panel.prefab",
                Fields = new[]
                {
                    new UIBindingField("imgAttribute", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("imgHpBg", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("imgHpFill", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("txtHpValue", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("imgExpBg", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("imgExpFill", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("txtExpValue", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtLv", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtLvText", typeof(UnityEngine.UI.Text)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UIBattleCombatPanel",
                PrefabName = "UI_BattleCombat_Panel",
                PrefabPath = "Assets/Prefabs/UI/BattleHUD/UI_BattleCombat_Panel.prefab",
                Fields = new[]
                {
                    new UIBindingField("btnSkillE", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnSkillQ", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnSkillR", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("btnSkillT", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("imgSkillT", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("imgSkill", typeof(UnityEngine.UI.Image), collectAll: true),
                    new UIBindingField("txtSkill", typeof(UnityEngine.UI.Text), collectAll: true),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UISkillWheelDialog",
                PrefabName = "UI_SkillWheel_Dialog",
                PrefabPath = "Assets/Prefabs/UI/BattleHUD/UI_SkillWheel_Dialog.prefab",
                Fields = new[]
                {
                    new UIBindingField("palSkill1", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill2", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill3", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill4", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill5", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill6", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill7", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("palSkill8", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("txtName", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtTip", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("imgMask", typeof(UnityEngine.UI.Image)),
                    new UIBindingField("imgBg", typeof(UnityEngine.UI.Image)),
                }
            },
            new UIBindingViewSpec
            {
                ClassName = "UISkillSelectPanel",
                PrefabName = "UI_SkillSelect_Panel",
                PrefabPath = "Assets/Prefabs/UI/BattleHUD/UI_SkillSelect_Panel.prefab",
                Fields = new[]
                {
                    new UIBindingField("btnSelect", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("txtTitle", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("txtSelect", typeof(UnityEngine.UI.Text)),
                    new UIBindingField("palCard0", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("palCard1", typeof(UnityEngine.UI.Button)),
                    new UIBindingField("palCard2", typeof(UnityEngine.UI.Button)),
                }
            },
        };
    }
}
