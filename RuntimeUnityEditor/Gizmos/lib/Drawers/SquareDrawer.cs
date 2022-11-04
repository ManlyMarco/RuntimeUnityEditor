using UnityEngine;

namespace Popcron
{
    public class SquareDrawer : Drawer
    {
		public SquareDrawer()
		{
			
		}
		
        public override int Draw(ref Vector3[] buffer, params object[] values)
        {
            Vector2 position = default;
            if (values[0] is Vector2 p2)
            {
                position = p2;
            }
            else if (values[0] is Vector3 p3)
            {
                position = p3;
            }

            Quaternion rotation = (Quaternion)values[1];

            Vector2 size = default;
            if (values[2] is Vector2 s2)
            {
                size = s2;
            }
            else if (values[2] is Vector3 s3)
            {
                size = s3;
            }

            size *= 0.5f;

            Vector2 point1 = new Vector3(position.x - size.x, position.y - size.y);
            Vector2 point2 = new Vector3(position.x + size.x, position.y - size.y);
            Vector2 point3 = new Vector3(position.x + size.x, position.y + size.y);
            Vector2 point4 = new Vector3(position.x - size.x, position.y + size.y);

            point1 = rotation * (point1 - position);
            point1 += position;

            point2 = rotation * (point2 - position);
            point2 += position;

            point3 = rotation * (point3 - position);
            point3 += position;

            point4 = rotation * (point4 - position);
            point4 += position;

            //square
            buffer[0] = point1;
            buffer[1] = point2;

            buffer[2] = point2;
            buffer[3] = point3;

            buffer[4] = point3;
            buffer[5] = point4;

            //loop back to start
            buffer[6] = point4;
            buffer[7] = point1;

            return 8;
        }
    }
}
