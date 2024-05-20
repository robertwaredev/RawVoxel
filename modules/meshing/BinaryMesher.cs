using System.Collections;

namespace RawVoxel {
    public static class BinaryMesher { 
        public static void Generate(BitArray masks, int shifts) {
            // COLUMNS
            //  
            //  ---------   ---------   ---------
            //  | | | | |   | | | | |   | | | | |
            //  | | X | |   | | Y | |   | | Z | |
            //  | | | | |   | | | | |   | | | | |
            //  ---------   ---------   ---------

            // FACES
            //
            //  ---fwd---   ---bck---   ---fwd---   ---bck---   ---fwd---   ---bck---
            //  ↑ ↑ ↑ ↑ ↑   ↓ ↓ ↓ ↓ ↓   ↑ ↑ ↑ ↑ ↑   ↓ ↓ ↓ ↓ ↓   ↑ ↑ ↑ ↑ ↑   ↓ ↓ ↓ ↓ ↓
            //  ↑ ↑ X ↑ ↑   ↓ ↓ X ↓ ↓   ↑ ↑ Y ↑ ↑   ↓ ↓ Y ↓ ↓   ↑ ↑ Z ↑ ↑   ↓ ↓ Z ↓ ↓
            //  ↑ ↑ ↑ ↑ ↑   ↓ ↓ ↓ ↓ ↓   ↑ ↑ ↑ ↑ ↑   ↓ ↓ ↓ ↓ ↓   ↑ ↑ ↑ ↑ ↑   ↓ ↓ ↓ ↓ ↓
            //  ---------   ---------   ---------   ---------   ---------   ---------

            
            int[,,] columns = new int[3, 1 << shifts, 1 << shifts];
            int[,,] faces = new int[6, 1 << shifts, 1 << shifts];
            
            // Generate columns.
            for (int x = 0; x < 1 << shifts; x ++) {
                for (int y = 0; y < 1 << shifts; y ++) {
                    for (int z = 0; z < 1 << shifts; z ++) {
                        if (masks[(x << shifts << shifts) + (y << shifts) + z] == true) {      // Check visibility 
                            columns[0, y, z] |= 1 << x;  // x axis
                            columns[1, z, x] |= 1 << y;  // y axis
                            columns[2, x, y] |= 1 << z;  // z axis
                        }
                    }
                }
            }

            // Generate faces.
            for (int axis = 0; axis < 3; axis ++) {
                for (int l = 0; l < 1 << shifts; l ++) {
                    for (int r = 0; r < 1 << shifts; r ++) {
                        
                        int column = columns[axis, l, r];
                        
                        faces[2 * axis + 0, l, r] = column & ~(column << 1);    // Left to right
                        faces[2 * axis + 1, r, l] = column & ~(column >> 1);    // Right to left
                    }
                }
            }
        }
    }
}