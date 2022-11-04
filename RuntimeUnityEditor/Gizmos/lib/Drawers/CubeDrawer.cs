using UnityEngine;

namespace Popcron
{
    public class CubeDrawer : Drawer
    {
		public CubeDrawer()
		{
			
		}
		
        public override int Draw(ref Vector3[] buffer, params object[] values)
        {
            Vector3 position = (Vector3)values[0];
            Quaternion rotation = (Quaternion)values[1];
            Vector3 size = (Vector3)values[2];

            size *= 0.5f;

            Vector3 point1 = new Vector3(position.x - size.x, position.y - size.y, position.z - size.z);
            Vector3 point2 = new Vector3(position.x + size.x, position.y - size.y, position.z - size.z);
            Vector3 point3 = new Vector3(position.x + size.x, position.y + size.y, position.z - size.z);
            Vector3 point4 = new Vector3(position.x - size.x, position.y + size.y, position.z - size.z);

            Vector3 point5 = new Vector3(position.x - size.x, position.y - size.y, position.z + size.z);
            Vector3 point6 = new Vector3(position.x + size.x, position.y - size.y, position.z + size.z);
            Vector3 point7 = new Vector3(position.x + size.x, position.y + size.y, position.z + size.z);
            Vector3 point8 = new Vector3(position.x - size.x, position.y + size.y, position.z + size.z);

            point1 = rotation * (point1 - position);
            point1 += position;

            point2 = rotation * (point2 - position);
            point2 += position;

            point3 = rotation * (point3 - position);
            point3 += position;

            point4 = rotation * (point4 - position);
            point4 += position;

            point5 = rotation * (point5 - position);
            point5 += position;

            point6 = rotation * (point6 - position);
            point6 += position;

            point7 = rotation * (point7 - position);
            point7 += position;

            point8 = rotation * (point8 - position);
            point8 += position;

            //square
            buffer[0] = point1;
            buffer[1] = point2;

            buffer[2] = point2;
            buffer[3] = point3;

            buffer[4] = point3;
            buffer[5] = point4;

            buffer[6] = point4;
            buffer[7] = point1;

            //other square
            buffer[8] = point5;
            buffer[9] = point6;

            buffer[10] = point6;
            buffer[11] = point7;

            buffer[12] = point7;
            buffer[13] = point8;

            buffer[14] = point8;
            buffer[15] = point5;

            //connectors
            buffer[16] = point1;
            buffer[17] = point5;

            buffer[18] = point2;
            buffer[19] = point6;

            buffer[20] = point3;
            buffer[21] = point7;

            buffer[22] = point4;
            buffer[23] = point8;

            return 24;
        }
    }
}
