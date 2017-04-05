using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Graphing;
using System.Reflection;

namespace UnityEditor.VFX
{
    [Serializable]
    class VFXSlot : VFXModel<VFXSlot, VFXSlot>
    {
        public enum Direction
        {
            kInput,
            kOutput,
        }

        public Direction direction      { get { return m_Direction; } }
        public VFXProperty property     { get { return m_Property; } }
        public override string name     { get { return m_Property.name; } }

        public object value 
        { 
            get { return m_Value; }
            set
            {
                if (m_Value != value)
                {
                    m_Value = value;
                    owner.Invalidate(InvalidationCause.kParamChanged);
                    // TODO Update default expression values
                }
            }       
        }    

        public VFXExpression expression 
        {
            get 
            {
                InitializeExpressionTreeIfNeeded();
                return m_OutExpression; 
            }
            set { SetExpression(value); }
        }

        // Explicit setter to be able to not notify
        public void SetExpression(VFXExpression expr, bool notify = true)
        {
            //if (direction == Direction.kInput)
            //    throw new InvalidOperationException("Explicit SetExpression can only be called on output slots");

            if (m_LinkedInExpression != expr)
            {
                InitializeExpressionTreeIfNeeded();
                m_LinkedInExpression = expr;
                RecomputeExpressionTree(true,notify);
            }
        }

        public ReadOnlyCollection<VFXSlot> LinkedSlots
        {
            get
            {
                return m_LinkedSlots.AsReadOnly();
            }
        }

        public VFXSlot refSlot
        { 
            get 
            {
                if (direction == Direction.kOutput || !HasLink())
                    return this;
                return m_LinkedSlots[0];
            } 
        }

        public IVFXSlotContainer owner { get { return m_Owner as IVFXSlotContainer; } }

        public VFXSlot GetTopMostParent() // TODO Cache this instead of walking the hierarchy every time
        {
            if (GetParent() == null)
                return this;
            else
                return GetParent().GetTopMostParent();
        }

        protected VFXSlot() {} // For serialization only

        // Create and return a slot hierarchy from a property info
        public static VFXSlot Create(VFXProperty property, Direction direction, object value = null)
        {
            var slot = CreateSub(property, direction, value); // First create slot tree
            slot.RecomputeExpressionTree(); // Initialize expressions   
            return slot;
        }
     
        private static VFXSlot CreateSub(VFXProperty property, Direction direction, object value)
        {
            var desc = VFXLibrary.GetSlot(property.type);
            if (desc != null)
            {
                var slot = desc.CreateInstance();
                slot.m_Direction = direction;
                slot.m_Property = property;
                slot.m_Value = value;

                foreach (var subInfo in property.SubProperties())
                {
                    var subSlot = CreateSub(subInfo, direction, null);
                    if (subSlot != null)
                        subSlot.Attach(slot,false);
                }

                return slot;
            }

            throw new InvalidOperationException(string.Format("Unable to create slot for property {0} of type {1}",property.name,property.type));
        }

        private void InitDefaultExpression()
        {
            if (GetNbChildren() == 0)
            {
                m_DefaultExpression = DefaultExpression();
            }
            else
            {
                // Depth first
                foreach (var child in children)
                    child.InitDefaultExpression();

                m_DefaultExpression = ExpressionFromChildren(children.Select(c => c.m_DefaultExpression).ToArray());
            }

            m_LinkedInExpression = m_InExpression = m_OutExpression = m_DefaultExpression;
        }

        private void ResetExpression()
        {
            if (GetNbChildren() == 0)
                SetExpression(m_DefaultExpression,false);
            else
            {
                foreach (var child in children)
                    child.ResetExpression();
            }  
        }

        protected override void Invalidate(VFXModel model,InvalidationCause cause)
        {
            // do nothing for slots
        }

        protected override void OnAdded()
        {
            base.OnAdded();

        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
        }

        public override T Clone<T>()
        {
            var clone = base.Clone<T>();
            var cloneSlot = clone as VFXSlot;

            cloneSlot.m_LinkedSlots.Clear();
            return clone;
        }
    
        public int GetNbLinks() { return m_LinkedSlots.Count; }
        public bool HasLink() { return GetNbLinks() != 0; }
        
        public bool CanLink(VFXSlot other)
        {
            return direction != other.direction && !m_LinkedSlots.Contains(other) && 
                ((direction == Direction.kInput && CanConvertFrom(other.expression)) || (other.CanConvertFrom(expression)));
        }

        public bool Link(VFXSlot other, bool notify = true)
        {
            if (other == null)
                return false;

            if (!CanLink(other) || !other.CanLink(this)) // can link
                return false;

            if (direction == Direction.kOutput)
                InnerLink(this, other, notify);
            else
                InnerLink(other, this, notify);

            return true;
        }

        public void Unlink(VFXSlot other, bool notify = true)
        {
            if (m_LinkedSlots.Contains(other))
            {
                if (direction == Direction.kOutput)
                    InnerUnlink(this, other, notify);
                else
                    InnerUnlink(other, this, notify);
            }
        }

        protected void PropagateToOwner(Action<IVFXSlotContainer> func)
        {
            if (owner != null)
                func(owner);
            else
            {
                var parent = GetParent();
                if (parent != null)
                    parent.PropagateToOwner(func);
            }
        }

        protected void PropagateToParent(Action<VFXSlot> func)
        {
            var parent = GetParent();
            if (parent != null)
            {
                func(parent);
                parent.PropagateToParent(func);   
            }
        }

        protected void PropagateToChildren(Action<VFXSlot> func)
        {
            func(this);
            foreach (var child in children) 
                child.PropagateToChildren(func);
        }

        protected void PropagateToTree(Action<VFXSlot> func)
        {
            PropagateToParent(func);
            PropagateToChildren(func);
        }


        protected IVFXSlotContainer GetOwner()
        {
            var parent = GetParent();
            if (parent != null)
                return parent.GetOwner();
            else
                return owner;
        }

        public void Initialize()
        {
           /* if (m_Initialize)
                return;

            var roots = new List<IVFXSlotContainer>();
            var visited = new HashSet<IVFXSlotContainer>();
            GatherUninitializedRoots(this.GetOwner(), roots, visited);

            foreach (var container in roots)
                container.UpdateOutputs();*/
        }

        private static void GatherUninitializedRoots(IVFXSlotContainer currentContainer, List<IVFXSlotContainer> roots, HashSet<IVFXSlotContainer> visited)
        {
            if (visited.Contains(currentContainer))
                return;

            visited.Add(currentContainer);
            if (currentContainer.GetNbInputSlots() == 0)
            {
                roots.Add(currentContainer);
                return;
            }

            foreach (var input in currentContainer.inputSlots)
                if (!input.m_Initialize)
                {
                    var owner = input.GetOwner();
                    if (owner != null)
                        GatherUninitializedRoots(owner, roots, visited);
                }
        }

        public void InitializeExpressionTreeIfNeeded()
        {
            if (!m_Initialize)
                RecomputeExpressionTree(false, false);
        }

        private void RecomputeExpressionTree(bool propagate = false,bool notify = true)
        {
            VFXSlot toUnlink = null;
            while ((toUnlink = TryRecomputeExpressionTree(propagate, notify)) != null)
            {
                if (toUnlink.direction == Direction.kOutput)
                    throw new InvalidOperationException("Set an invalid input expression to output slot");

                Debug.Log(string.Format("Invalid connection when recomputing expression for slot {0}", toUnlink.DebugName));
                toUnlink.UnlinkAll();
            }
        }

        // Return slot to unlink in case of issue
        private VFXSlot TryRecomputeExpressionTree(bool propagate = false,bool notify = true)
        {
            // Start from the top most parent
            var masterSlot = GetTopMostParent();

            // For input slots, linked slots needs to be initialized
            if (!m_Initialize)
            {
                if (direction == Direction.kInput)
                {
                    var outputs = new HashSet<VFXSlot>();
                    masterSlot.PropagateToChildren(s => {
                        if (HasLink())
                            outputs.Add(refSlot.GetTopMostParent());
                    });

                    foreach (var output in outputs)
                        if (!output.m_Initialize)
                            output.RecomputeExpressionTree(false, false);
                }

                InitDefaultExpression();
                masterSlot.PropagateToChildren(s => s.m_Initialize = true);
            }

            if (direction == Direction.kInput) // For input slots, linked expression are directly taken from linked slots
                masterSlot.PropagateToChildren(s => s.m_LinkedInExpression = s.HasLink() ? s.refSlot.m_OutExpression : s.m_DefaultExpression);

            bool needsRecompute = false;
            masterSlot.PropagateToChildren(s =>
            {
                if (s.m_LinkedInExpression != s.m_CachedLinkedInExpression)
                {
                    s.m_CachedLinkedInExpression = s.m_LinkedInExpression;
                    needsRecompute = true;
                }
            });

            if (!needsRecompute) // We dont need to recompute, tree is already up to date
                return null;

            Debug.Log("RECOMPUTE EXPRESSION TREE FOR " + DebugName);

            List<VFXSlot> startSlots = new List<VFXSlot>();
            List<VFXSlot> toUnlink = new List<VFXSlot>();

            masterSlot.PropagateToChildren( s => {
                if (s.m_LinkedInExpression != s.m_DefaultExpression) 
                    startSlots.Add(s); 
            });

            if (startSlots.Count == 0) // Default expression
                masterSlot.PropagateToChildren(s => s.m_InExpression = s.m_DefaultExpression );
            else
            {
                // Check if everything is right
                foreach (var startSlot in startSlots)
                    if (!startSlot.CanConvertFrom(startSlot.m_LinkedInExpression))
                        return startSlot; // We need to unlink

                // build expression trees by propagating from start slots
                foreach (var startSlot in startSlots)
                {
                    var newExpression = startSlot.ConvertExpression(startSlot.m_LinkedInExpression); // TODO Handle structural modification

                    // TODO Is that correct ?
                    //if (newExpression == startSlot.m_InExpression) // already correct, early out
                    //    continue;

                    startSlot.m_InExpression = newExpression;
                    startSlot.PropagateToParent(s => s.m_InExpression = s.ExpressionFromChildren(s.children.Select(c => c.m_InExpression).ToArray()));

                    startSlot.PropagateToChildren(s =>
                    {
                        var exp = s.ExpressionToChildren(s.m_InExpression);
                        for (int i = 0; i < s.GetNbChildren(); ++i)
                            s.GetChild(i).m_InExpression = exp != null ? exp[i] : s.refSlot.GetChild(i).expression;
                    });
                }
            }
      
            List<VFXSlot> toPropagate = direction == Direction.kOutput ? new List<VFXSlot>() : null;

            // Finally derive output expressions
            if (masterSlot.SetOutExpression(masterSlot.m_InExpression) && toPropagate != null)
                toPropagate.AddRange(masterSlot.LinkedSlots);

            masterSlot.PropagateToChildren(s => {
                var exp = s.ExpressionToChildren(s.m_OutExpression);
                for (int i = 0; i < s.GetNbChildren(); ++i)
                {
                    var child = s.GetChild(i);
                    if (child.SetOutExpression(exp != null ? exp[i] : child.m_InExpression) && toPropagate != null)
                        toPropagate.AddRange(child.LinkedSlots);
                }
            });  
 
            if (notify && masterSlot.m_Owner != null)
                masterSlot.m_Owner.Invalidate(InvalidationCause.kStructureChanged);

            if (direction == Direction.kOutput)
            {
                var dirtyMasterSlots = new HashSet<VFXSlot>(toPropagate.Select(s => s.GetTopMostParent()));
                foreach (var dirtySlot in dirtyMasterSlots)
                    dirtySlot.RecomputeExpressionTree(notify);
            }

            return null;
        }

        private void NotifyOwner(InvalidationCause cause)
        {
            PropagateToOwner(o => o.Invalidate(cause));
        }

        private bool SetOutExpression(VFXExpression expr)
        {
            if (m_OutExpression != expr)
            {
                m_OutExpression = expr;

                if (direction == Direction.kOutput)
                {
                    var toRemove = LinkedSlots.Where(s => !s.CanConvertFrom(expr)); // Break links that are no more valid
                    foreach (var slot in toRemove)
                    {
                        Debug.Log(string.Format("Invalid link between {0} and {1} - Break it!", DebugName, slot.DebugName));
                        Unlink(slot);
                    }
                }
            }
            return true;
        }

        public void UnlinkAll(bool notify = true)
        {
            var currentSlots = new List<VFXSlot>(m_LinkedSlots);
            foreach (var slot in currentSlots)
                Unlink(slot,notify);
        }

        private static void InnerLink(VFXSlot output,VFXSlot input,bool notify = false)
        {
            input.UnlinkAll(false); // First disconnect any other linked slot
            input.PropagateToTree(s => s.UnlinkAll(false)); // Unlink other links in tree
            
            input.m_LinkedSlots.Add(output);
            output.m_LinkedSlots.Add(input);

            input.RecomputeExpressionTree(notify);
        }

        private static void InnerUnlink(VFXSlot output, VFXSlot input, bool notify = false)
        {
            if (input.m_LinkedSlots.Remove(output))
                input.RecomputeExpressionTree(true, notify);
            output.m_LinkedSlots.Remove(input);
        }

        /*protected override void OnInvalidate(VFXModel model, InvalidationCause cause)
        {
            if (cause == VFXModel.InvalidationCause.kStructureChanged)
            {
                if (HasLink())
                    throw new InvalidOperationException();

                SetInExpression(ExpressionFromChildren(children.Select(c => c.m_InExpression).ToArray()));
            }
        }*/

        protected virtual bool CanConvertFrom(VFXExpression expr)
        {
            return expr == null || m_DefaultExpression.ValueType == expr.ValueType;
        }

        protected virtual VFXExpression ConvertExpression(VFXExpression expression)
        {
            return expression;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (m_LinkedSlots == null)
            {
                m_LinkedSlots = new List<VFXSlot>();
            }
        }

        public override void OnBeforeSerialize()
        {
            base.OnBeforeSerialize();
            if (m_Value != null)
            {
                //m_SerializableValue = SerializationHelper.Serialize(m_Value);
                m_SerializedValue = VFXSerializer.Save(m_Value);
                m_SerializedValueAssembly = m_Value.GetType().Assembly.FullName;
                m_SerializedValueType = m_Value.GetType().FullName;
            }
        }

         public override void OnAfterDeserialize()
         {
            base.OnAfterDeserialize();
            //if (!m_SerializableValue.Empty)
            if( ! string.IsNullOrEmpty( m_SerializedValue ))
            {
                //m_Value = SerializationHelper.Deserialize<object>(m_SerializableValue, null);

                var assembly = Assembly.Load(m_SerializedValueAssembly);
                if( assembly == null)
                {
                    Debug.LogError("Can't load assembly "+m_SerializedValueAssembly+" for type" + m_SerializedValueType);
                    return;
                }

                System.Type type = assembly.GetType(m_SerializedValueType);
                if( type == null)
                {
                    Debug.LogError("Can't find type "+m_SerializedValueType+" in assembly" + m_SerializedValueAssembly);
                    return;
                }


                m_Value = VFXSerializer.Load(type, m_SerializedValue);



                m_Value = VFXSerializer.Load(m_Value.GetType(), m_SerializedValue);
            }
            //m_SerializableValue.Clear();
        }

        protected virtual VFXExpression[] ExpressionToChildren(VFXExpression exp)   { return null; }
        protected virtual VFXExpression ExpressionFromChildren(VFXExpression[] exp) { return null; }

        protected virtual VFXValue DefaultExpression() 
        {
            return null; 
        }

        // Expression cache
        private VFXExpression m_DefaultExpression; // The default expression
        private VFXExpression m_LinkedInExpression; // The current linked expression to the slot
        private VFXExpression m_CachedLinkedInExpression; // Cached footprint of latest recompute tree
        private VFXExpression m_InExpression; // correctly converted expression
        private VFXExpression m_OutExpression; // output expression that can be fetched

        [NonSerialized] // This must not survive domain reload !
        private bool m_Initialize = false;

        // TODO currently not used
        [Serializable]
        private class MasterData : ISerializationCallbackReceiver
        {
            public VFXModel m_Owner;
            [NonSerialized]
            public object m_Value;
            [SerializeField]
            public SerializationHelper.JSONSerializedElement m_SerializedValue;

            public virtual void OnBeforeSerialize()
            {
                if (m_Value != null)
                    m_SerializedValue = SerializationHelper.Serialize(m_Value);
                else
                    m_SerializedValue.Clear();
            }

            public virtual void OnAfterDeserialize()
            {
                m_Value = !m_SerializedValue.Empty ? SerializationHelper.Deserialize<object>(m_SerializedValue, null) : null;
            }
        }

        [SerializeField]
        private VFXSlot m_MasterSlot;
        [SerializeField]
        private MasterData m_MasterData;

        [SerializeField]
        public VFXModel m_Owner;

        [SerializeField]
        private VFXProperty m_Property;

        [SerializeField]
        private Direction m_Direction;

        [SerializeField]
        private List<VFXSlot> m_LinkedSlots;

        //[SerializeField]
        //private SerializationHelper.JSONSerializedElement m_SerializableValue;

        [SerializeField]
        private string m_SerializedValue;

        [SerializeField]
        private string m_SerializedValueType;

        [SerializeField]
        private string m_SerializedValueAssembly;

        [NonSerialized]
        protected object m_Value;
    }
}
