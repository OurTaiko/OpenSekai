using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Events;

namespace UiEffect
{
	public class UiEffectObjectPool<T> where T : new()
	{
		private readonly Stack<T> m_Stack;

		private readonly UnityAction<T> m_ActionOnGet;

		private readonly UnityAction<T> m_ActionOnRelease;

		public int countAll
		{
			[CompilerGenerated]
			get;
			[CompilerGenerated]
			private set;
		}

		public int countActive
		{
			get
			{
				return countAll - countInactive;
			}
		}

		public int countInactive
		{
			get
			{
				return m_Stack.Count;
			}
		}

		public UiEffectObjectPool(UnityAction<T> actionOnGet, UnityAction<T> actionOnRelease)
		{
			m_Stack = new Stack<T>();
			m_ActionOnGet = actionOnGet;
			m_ActionOnRelease = actionOnRelease;
		}

		public T Get()
		{
			T element;
			if (m_Stack.Count == 0)
			{
				element = new T();
				countAll++;
			}
			else
			{
				element = m_Stack.Pop();
			}

			m_ActionOnGet?.Invoke(element);
			return element;
		}

		public void Release(T element)
		{
			if (m_Stack.Count > 0 && ReferenceEquals(m_Stack.Peek(), element))
			{
				return;
			}

			m_ActionOnRelease?.Invoke(element);
			m_Stack.Push(element);
		}
	}
}
