using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using AView = Android.Views.View;
using AListView = Android.Widget.ListView;

namespace Xamarin.Forms.Platform.Android
{
	public abstract class CellAdapter : BaseAdapter<object>, AdapterView.IOnItemLongClickListener, ActionMode.ICallback, AdapterView.IOnItemClickListener,
										global::Android.Support.V7.View.ActionMode.ICallback
	{
		readonly Context _context;
		ActionMode _actionMode;
		Cell _actionModeContext;

		bool _actionModeNeedsUpdates;
		AView _contextView;
		global::Android.Support.V7.View.ActionMode _supportActionMode;

		protected CellAdapter(Context context)
		{
			if (context == null)
				throw new ArgumentNullException("context");

			_context = context;
		}

		internal Cell ActionModeContext
		{
			get { return _actionModeContext; }
			set
			{
				if (_actionModeContext == value)
					return;

				if (_actionModeContext != null)
					((INotifyCollectionChanged)_actionModeContext.ContextActions).CollectionChanged -= OnContextItemsChanged;

				ActionModeObject = null;
				_actionModeContext = value;

				if (_actionModeContext != null)
				{
					((INotifyCollectionChanged)_actionModeContext.ContextActions).CollectionChanged += OnContextItemsChanged;
					ActionModeObject = _actionModeContext.BindingContext;
				}
			}
		}

		internal object ActionModeObject { get; set; }

		internal AView ContextView
		{
			get { return _contextView; }
			set
			{
				if (_contextView == value)
					return;

				if (_contextView != null)
				{
					var isSelected = (bool)ActionModeContext.GetValue(ListViewAdapter.IsSelectedProperty);
					if (isSelected)
						SetSelectedBackground(_contextView);
					else
						UnsetSelectedBackground(_contextView);
				}

				_contextView = value;

				if (_contextView != null)
					SetSelectedBackground(_contextView, true);
			}
		}

		public bool OnActionItemClicked(ActionMode mode, IMenuItem item)
		{
			OnActionItemClickedImpl(item);
			if (mode != null && mode.Handle != IntPtr.Zero)
				mode.Finish();
			return true;
		}

		bool global::Android.Support.V7.View.ActionMode.ICallback.OnActionItemClicked(global::Android.Support.V7.View.ActionMode mode, IMenuItem item)
		{
			OnActionItemClickedImpl(item);
			mode.Finish();
			return true;
		}

		public bool OnCreateActionMode(ActionMode mode, IMenu menu)
		{
			CreateContextMenu(menu);
			return true;
		}

		bool global::Android.Support.V7.View.ActionMode.ICallback.OnCreateActionMode(global::Android.Support.V7.View.ActionMode mode, IMenu menu)
		{
			CreateContextMenu(menu);
			return true;
		}

		public void OnDestroyActionMode(ActionMode mode)
		{
			OnDestroyActionModeImpl();
			_actionMode.Dispose();
			_actionMode = null;
		}

		void global::Android.Support.V7.View.ActionMode.ICallback.OnDestroyActionMode(global::Android.Support.V7.View.ActionMode mode)
		{
			OnDestroyActionModeImpl();
			_supportActionMode.Dispose();
			_supportActionMode = null;
		}

		public bool OnPrepareActionMode(ActionMode mode, IMenu menu)
		{
			return OnPrepareActionModeImpl(menu);
		}

		bool global::Android.Support.V7.View.ActionMode.ICallback.OnPrepareActionMode(global::Android.Support.V7.View.ActionMode mode, IMenu menu)
		{
			return OnPrepareActionModeImpl(menu);
		}

		public void OnItemClick(AdapterView parent, AView view, int position, long id)
		{
			if (_actionMode != null || _supportActionMode != null)
			{
				var listView = parent as AListView;
				if (listView != null)
					position -= listView.HeaderViewsCount;
				HandleContextMode(view, position);
			}
			else
				HandleItemClick(parent, view, position, id);
		}

		public bool OnItemLongClick(AdapterView parent, AView view, int position, long id)
		{
			var listView = parent as AListView;
			if (listView != null)
				position -= listView.HeaderViewsCount;
			return HandleContextMode(view, position);
		}

		protected abstract Cell GetCellForPosition(int position);

		protected virtual void HandleItemClick(AdapterView parent, AView view, int position, long id)
		{
		}

		protected void SetSelectedBackground(AView view, bool isContextTarget = false)
		{
			int attribute = isContextTarget ? global::Android.Resource.Attribute.ColorLongPressedHighlight : global::Android.Resource.Attribute.ColorActivatedHighlight;
			using (var value = new TypedValue())
			{
				if (_context.Theme.ResolveAttribute(attribute, value, true))
					view.SetBackgroundResource(value.ResourceId);
				else
					view.SetBackgroundResource(global::Android.Resource.Color.HoloBlueDark);
			}
		}

		protected void UnsetSelectedBackground(AView view)
		{
			view.SetBackgroundResource(0);
		}

		internal void CloseContextAction()
		{
			if (_actionMode != null)
				_actionMode.Finish();
			if (_supportActionMode != null)
				_supportActionMode.Finish();
		}

		void CreateContextMenu(IMenu menu)
		{
			var changed = new PropertyChangedEventHandler(OnContextActionPropertyChanged);
			var changing = new PropertyChangingEventHandler(OnContextActionPropertyChanging);
			var commandChanged = new EventHandler(OnContextActionCommandCanExecuteChanged);

			for (var i = 0; i < ActionModeContext.ContextActions.Count; i++)
			{
				MenuItem action = ActionModeContext.ContextActions[i];

				IMenuItem item = menu.Add(Menu.None, i, Menu.None, action.Text);

				if (action.Icon != null)
					item.SetIcon(_context.Resources.GetDrawable(action.Icon));

				action.PropertyChanged += changed;
				action.PropertyChanging += changing;

				if (action.Command != null)
					action.Command.CanExecuteChanged += commandChanged;

				if (!((IMenuItemController)action).IsEnabled)
					item.SetEnabled(false);
			}
		}

		bool HandleContextMode(AView view, int position)
		{
			Cell cell = GetCellForPosition(position);

			if (cell == null)
				return false;

			if (_actionMode != null || _supportActionMode != null)
			{
				if (!cell.HasContextActions)
				{
					_actionMode?.Finish();
					_supportActionMode?.Finish();
					return false;
				}

				ActionModeContext = cell;

				_actionMode?.Invalidate();
				_supportActionMode?.Invalidate();
			}
			else
			{
				if (!cell.HasContextActions)
					return false;

				ActionModeContext = cell;

				var appCompatActivity = Forms.Context as FormsAppCompatActivity;
				if (appCompatActivity == null)
					_actionMode = ((Activity)Forms.Context).StartActionMode(this);
				else
					_supportActionMode = appCompatActivity.StartSupportActionMode(this);
			}

			ContextView = view;

			return true;
		}

		void OnActionItemClickedImpl(IMenuItem item)
		{
			int index = item.ItemId;
			IMenuItemController action = ActionModeContext.ContextActions[index];

			action.Activate();
		}

		void OnContextActionCommandCanExecuteChanged(object sender, EventArgs eventArgs)
		{
			_actionModeNeedsUpdates = true;
			_actionMode.Invalidate();
		}

		void OnContextActionPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var action = (MenuItem)sender;

			if (e.PropertyName == MenuItem.CommandProperty.PropertyName)
			{
				if (action.Command != null)
					action.Command.CanExecuteChanged += OnContextActionCommandCanExecuteChanged;
			}
			else
				_actionModeNeedsUpdates = true;
		}

		void OnContextActionPropertyChanging(object sender, PropertyChangingEventArgs e)
		{
			var action = (MenuItem)sender;

			if (e.PropertyName == MenuItem.CommandProperty.PropertyName)
			{
				if (action.Command != null)
					action.Command.CanExecuteChanged -= OnContextActionCommandCanExecuteChanged;
			}
		}

		void OnContextItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			_actionModeNeedsUpdates = true;
			_actionMode.Invalidate();
		}

		void OnDestroyActionModeImpl()
		{
			var changed = new PropertyChangedEventHandler(OnContextActionPropertyChanged);
			var changing = new PropertyChangingEventHandler(OnContextActionPropertyChanging);
			var commandChanged = new EventHandler(OnContextActionCommandCanExecuteChanged);

			((INotifyCollectionChanged)ActionModeContext.ContextActions).CollectionChanged -= OnContextItemsChanged;

			for (var i = 0; i < ActionModeContext.ContextActions.Count; i++)
			{
				MenuItem action = ActionModeContext.ContextActions[i];
				action.PropertyChanged -= changed;
				action.PropertyChanging -= changing;

				if (action.Command != null)
					action.Command.CanExecuteChanged -= commandChanged;
			}
			ContextView = null;

			ActionModeContext = null;
			_actionModeNeedsUpdates = false;
		}

		bool OnPrepareActionModeImpl(IMenu menu)
		{
			if (_actionModeNeedsUpdates)
			{
				_actionModeNeedsUpdates = false;

				menu.Clear();
				CreateContextMenu(menu);
			}

			return false;
		}
	}
}