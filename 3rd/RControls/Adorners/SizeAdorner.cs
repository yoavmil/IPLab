using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RControls.Adorners
{
	public class SizeAdorner : Adorner
	{
		private SizeChrome chrome;
		private VisualCollection visuals;
		private ContentControl imageItem;

		protected override int VisualChildrenCount
		{
			get
			{
				return this.visuals.Count;
			}
		}

		public SizeAdorner(ContentControl imageItem)
			: base(imageItem)
		{
			this.SnapsToDevicePixels = true;
			this.imageItem = imageItem;
			this.chrome = new SizeChrome();
			this.chrome.DataContext = imageItem;
			this.visuals = new VisualCollection(this);
			this.visuals.Add(this.chrome);
		}

		protected override Visual GetVisualChild(int index)
		{
			return this.visuals[index];
		}

		protected override Size ArrangeOverride(Size arrangeBounds)
		{
			this.chrome.Arrange(new Rect(new Point(0.0, 0.0), arrangeBounds));
			return arrangeBounds;
		}
	}
}
