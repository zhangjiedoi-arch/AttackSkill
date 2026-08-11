using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttackSkill.UI
{
    public class CommonSureArgs
    {
        public string title;
        public string tip;
        public Action onSure;
        public Action onCancel;
    }

    public class CommonTipArgs
    {
        public string tip;
        public float duration = 1.5f;
    }
}
