// This is basically type *.cs >LZ4.txt from LZ4 folder

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace LZ4Sharp
{
    public interface ILZ4Compressor
    {
        int CalculateMaxCompressedLength(int uncompressedLength);
        byte[] Compress(byte[] source);
        int Compress(byte[] source, byte[] dest);
        int Compress(byte[] source, int srcOffset, int count, byte[] dest, int dstOffset);
    }

    public interface ILZ4Decompressor
    {
        unsafe int Decompress(byte* compressedBuffer, byte* decompressedBuffer, int compressedSize, int maxDecompressedSize);
        byte[] Decompress(byte[] compressed);
        int Decompress(byte[] compressed, byte[] decompressedBuffer);
        int Decompress(byte[] compressedBuffer, byte[] decompressedBuffer, int compressedSize);
        int Decompress(byte[] compressedBuffer, int compressedPosition, byte[] decompressedBuffer, int decompressedPosition, int compressedSize);
        unsafe int DecompressKnownSize(byte* compressed, byte* decompressedBuffer, int decompressedSize);
        void DecompressKnownSize(byte[] compressed, byte[] decompressed);
        int DecompressKnownSize(byte[] compressed, byte[] decompressedBuffer, int decompressedSize);
    }

    /// <summary>
    /// Static LZ4 Compression and Decompression. This is threadsafe because it creates new instances of the
    /// compression/decompression classes for each method call.
    /// It is recommended to use LZ4Compressor and LZ4Decompressor for repeated compressing/decompressing for speed and less memory allocations.
    /// </summary>
    public static unsafe class LZ4
    {
        public static byte[] Compress(byte[] source)
        {
            return LZ4CompressorFactory.CreateNew().Compress(source);
        }

        /// <summary>
        /// Calculate the max compressed byte[] size given the size of the uncompressed byte[]
        /// </summary>
        /// <param name="uncompressedLength">Length of the uncompressed data</param>
        /// <returns>The maximum required size in bytes of the compressed data</returns>
        public static int CalculateMaxCompressedLength(int uncompressedLength)
        {
            return LZ4CompressorFactory.CreateNew().CalculateMaxCompressedLength(uncompressedLength);
        }

        /// <summary>
        /// Compress source into dest returning compressed length
        /// </summary>
        /// <param name="source">uncompressed data</param>
        /// <param name="dest">array into which source will be compressed</param>
        /// <returns>compressed length</returns>
        public static int Compress(byte[] source, byte[] dest)
        {
            return LZ4CompressorFactory.CreateNew().Compress(source, dest);
        }

        /// <summary>
        /// Compress source into dest returning compressed length
        /// </summary>
        /// <param name="source">uncompressed data</param>
        /// <param name="srcOffset">offset in source array where reading will start</param>
        /// <param name="count">count of bytes in source array to compress</param>
        /// <param name="dest">array into which source will be compressed</param>
        /// <param name="dstOffset">start index in dest array where writing will start</param>
        /// <returns>compressed length</returns>
        public static int Compress(byte[] source, int srcOffset, int count, byte[] dest, int dstOffset)
        {
            return LZ4CompressorFactory.CreateNew().Compress(source, srcOffset, count, dest, dstOffset);
        }


        public static void DecompressKnownSize(byte[] compressed, byte[] decompressed)
        {
            LZ4DecompressorFactory.CreateNew().DecompressKnownSize(compressed, decompressed);
        }

        public static int DecompressKnownSize(byte[] compressed, byte[] decompressedBuffer, int decompressedSize)
        {
            return LZ4DecompressorFactory.CreateNew().DecompressKnownSize(compressed, decompressedBuffer, decompressedSize);
        }

        public static int DecompressKnownSize(byte* compressed, byte* decompressedBuffer, int decompressedSize)
        {
            return LZ4DecompressorFactory.CreateNew().DecompressKnownSize(compressed, decompressedBuffer, decompressedSize);
        }

        public static byte[] Decompress(byte[] compressed)
        {
            return LZ4DecompressorFactory.CreateNew().Decompress(compressed);
        }

        public static int Decompress(byte[] compressed, byte[] decompressedBuffer)
        {
            return LZ4DecompressorFactory.CreateNew().Decompress(compressed, decompressedBuffer);
        }

        public static int Decompress(byte[] compressedBuffer, byte[] decompressedBuffer, int compressedSize)
        {
            return LZ4DecompressorFactory.CreateNew().Decompress(compressedBuffer, decompressedBuffer, compressedSize);
        }

        public static int Decompress(
            byte* compressedBuffer,
            byte* decompressedBuffer,
            int compressedSize,
            int maxDecompressedSize)
        {
            return LZ4DecompressorFactory.CreateNew().Decompress(compressedBuffer, decompressedBuffer, compressedSize, maxDecompressedSize);
        }
    }


    /// <summary>
    /// Class for compressing a byte array into an LZ4 byte array.
    /// </summary>
 public unsafe class LZ4Compressor32 : ILZ4Compressor
    {
        //**************************************
        // Tuning parameters
        //**************************************
        // COMPRESSIONLEVEL :
        // Increasing this value improves compression ratio
        // Lowering this value reduces memory usage
        // Reduced memory usage typically improves speed, due to cache effect (ex : L1 32KB for Intel, L1 64KB for AMD)
        // Memory usage formula : N->2^(N+2) Bytes (examples : 12 -> 16KB ; 17 -> 512KB)
        const int COMPRESSIONLEVEL = 12;

        // NOTCOMPRESSIBLE_CONFIRMATION :
        // Decreasing this value will make the algorithm skip faster data segments considered "incompressible"
        // This may decrease compression ratio dramatically, but will be faster on incompressible data
        // Increasing this value will make the algorithm search more before declaring a segment "incompressible"
        // This could improve compression a bit, but will be slower on incompressible data
        // The default value (6) is recommended
        // 2 is the minimum value.
        const int NOTCOMPRESSIBLE_CONFIRMATION = 6;

        //**************************************
        // Constants
        //**************************************
        const int HASH_LOG = COMPRESSIONLEVEL;
        const int MAXD_LOG = 16;
        const int MAX_DISTANCE = ((1 << MAXD_LOG) - 1);
        const int MINMATCH = 4;
        const int MFLIMIT = (LZ4Util.COPYLENGTH + MINMATCH);
        const int MINLENGTH = (MFLIMIT + 1);
        const uint LZ4_64KLIMIT = ((1U << 16) + (MFLIMIT - 1));
        const int HASHLOG64K = (HASH_LOG + 1);
        const int HASHTABLESIZE = (1 << HASH_LOG);
        const int HASH_MASK = (HASHTABLESIZE - 1);
        const int LASTLITERALS = 5;
        const int SKIPSTRENGTH = (NOTCOMPRESSIBLE_CONFIRMATION > 2 ? NOTCOMPRESSIBLE_CONFIRMATION : 2);
        const int SIZE_OF_LONG_TIMES_TWO_SHIFT = 4;
        const int STEPSIZE = 4;
        static byte[] DeBruijnBytePos = new byte[32] { 0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1, 3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1 };
        //**************************************
        // Macros
        //**************************************
        byte[] m_HashTable;

        public LZ4Compressor32()
        {
            m_HashTable = new byte[HASHTABLESIZE * IntPtr.Size];
            if (m_HashTable.Length % 16 != 0)
                throw new Exception("Hash table size must be divisible by 16");
        }


        public byte[] Compress(byte[] source)
        {
            int maxCompressedSize = CalculateMaxCompressedLength(source.Length);
            byte[] dst = new byte[maxCompressedSize];
            int length = Compress(source, dst);
            byte[] dest = new byte[length];
            Buffer.BlockCopy(dst, 0, dest, 0, length);
            return dest;
        }

        /// <summary>
        /// Calculate the max compressed byte[] size given the size of the uncompressed byte[]
        /// </summary>
        /// <param name="uncompressedLength">Length of the uncompressed data</param>
        /// <returns>The maximum required size in bytes of the compressed data</returns>
        public int CalculateMaxCompressedLength(int uncompressedLength)
        {
   return uncompressedLength + (uncompressedLength / 255) + 16;
        }

        /// <summary>
        /// Compress source into dest returning compressed length
        /// </summary>
        /// <param name="source">uncompressed data</param>
        /// <param name="dest">array into which source will be compressed</param>
        /// <returns>compressed length</returns>
        public int Compress(byte[] source, byte[] dest)
        {
            fixed (byte* s = source)
            fixed (byte* d = dest)
            {
                if (source.Length < (int)LZ4_64KLIMIT)
                    return Compress64K(s, d, source.Length, dest.Length);
                return Compress(s, d, source.Length, dest.Length);
            }
        }

        /// <summary>
        /// Compress source into dest returning compressed length
        /// </summary>
        /// <param name="source">uncompressed data</param>
        /// <param name="srcOffset">offset in source array where reading will start</param>
        /// <param name="count">count of bytes in source array to compress</param>
        /// <param name="dest">array into which source will be compressed</param>
        /// <param name="dstOffset">start index in dest array where writing will start</param>
        /// <returns>compressed length</returns>
        public int Compress(byte[] source, int srcOffset, int count, byte[] dest, int dstOffset)
        {
            fixed (byte* s = &source[srcOffset])
            fixed (byte* d = &dest[dstOffset])
            {
                if (source.Length < (int)LZ4_64KLIMIT)
                    return Compress64K(s, d, count, dest.Length - dstOffset);
                return Compress(s, d, count, dest.Length - dstOffset);
            }
        }

        int Compress(byte* source, byte* dest, int isize, int maxOutputSize)
        {
            fixed (byte* hashTablePtr = m_HashTable)
            fixed (byte* deBruijnBytePos = DeBruijnBytePos)
            {
                Clear(hashTablePtr, sizeof(byte*) * HASHTABLESIZE);
                byte** hashTable = (byte**)hashTablePtr;

                byte* ip = (byte*)source;
                int basePtr = 0;;

                byte* anchor = ip;
                byte* iend = ip + isize;
                byte* mflimit = iend - MFLIMIT;
                byte* matchlimit = (iend - LASTLITERALS);
    byte* oend = dest + maxOutputSize;


                byte* op = (byte*)dest;

                int len, length;
                const int skipStrength = SKIPSTRENGTH;
                uint forwardH;


                // Init
                if (isize < MINLENGTH) goto _last_literals;

                // First Byte
                hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))] = ip - basePtr;
                ip++; forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG));

                // Main Loop
                for (; ; )
                {
                    uint findMatchAttempts = (1U << skipStrength) + 3;
                    byte* forwardIp = ip;
                    byte* r;
                    byte* token;

                    // Find a match
                    do
                    {
                        uint h = forwardH;
                        uint step = findMatchAttempts++ >> skipStrength;
                        ip = forwardIp;
                        forwardIp = ip + step;

                        if (forwardIp > mflimit) { goto _last_literals; }

                        // LZ4_HASH_VALUE
                        forwardH = (((*(uint*)forwardIp) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG));
                        r = hashTable[h] + basePtr;
                        hashTable[h] = ip - basePtr;

                    } while ((r < ip - MAX_DISTANCE) || (*(uint*)r != *(uint*)ip));

                    // Catch up
                    while ((ip > anchor) && (r > (byte*)source) && (ip[-1] == r[-1])) { ip--; r--; }

                    // Encode Literal Length
                    length = (int)(ip - anchor);
                    token = op++;
                    if (length >= (int)LZ4Util.RUN_MASK) { *token = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); len = (int)(length - LZ4Util.RUN_MASK); for (; len > 254; len -= 255) *op++ = 255; *op++ = (byte)len; }
                    else *token = (byte)(length << LZ4Util.ML_BITS);

                    //Copy Literals
                    { byte* e=(op)+length; do { *(uint*)op = *(uint*)anchor; op+=4; anchor+=4;; *(uint*)op = *(uint*)anchor; op+=4; anchor+=4;; } while (op<e);; op=e; };

                _next_match:
                    // Encode Offset
                    *(ushort*)op = (ushort)(ip - r); op += 2;

                    // Start Counting
                    ip += MINMATCH; r += MINMATCH; // MinMatch verified
                    anchor = ip;
                    //					while (*(uint *)r == *(uint *)ip)
                    //					{
                    //						ip+=4; r+=4;
                    //						if (ip>matchlimit-4) { r -= ip - (matchlimit-3); ip = matchlimit-3; break; }
                    //					}
                    //					if (*(ushort *)r == *(ushort *)ip) { ip+=2; r+=2; }
                    //					if (*r == *ip) ip++;

                    while (ip < matchlimit - (STEPSIZE -1))
                    {
                        int diff = (int)(*(int*)(r) ^ *(int*)(ip));
                        if (diff == 0) { ip += STEPSIZE; r += STEPSIZE; continue; }
                        ip += DeBruijnBytePos[((uint)((diff & -diff) * 0x077CB531U)) >> 27];;
                        goto _endCount;
                    }




                    if ((ip < (matchlimit - 1)) && (*(ushort*)(r) == *(ushort*)(ip))) { ip += 2; r += 2; }
                    if ((ip < matchlimit) && (*r == *ip)) ip++;
                _endCount:

                    len = (int)(ip - anchor);
  if (op + (1 + LASTLITERALS) + (len>>8) >= oend) return 0; // Check output limit
                    // Encode MatchLength
                    if (len >= (int)LZ4Util.ML_MASK) { *token += (byte)LZ4Util.ML_MASK; len -= (byte)LZ4Util.ML_MASK; for (; len > 509; len -= 510) { *op++ = 255; *op++ = 255; } if (len > 254) { len -= 255; *op++ = 255; } *op++ = (byte)len; }
                    else *token += (byte)len;

                    // Test end of chunk
                    if (ip > mflimit) { anchor = ip; break; }

                    // Fill table
                    hashTable[(((*(uint*)ip-2) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))] = ip - 2 - basePtr;

                    // Test next position
                    r = basePtr + hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))];
                    hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))] = ip - basePtr;
                    if ((r > ip - (MAX_DISTANCE + 1)) && (*(uint*)r == *(uint*)ip)) { token = op++; *token = 0; goto _next_match; }

                    // Prepare next loop
                    anchor = ip++;
                    forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG));
                }

            _last_literals:
                // Encode Last Literals
                {
                    int lastRun = (int)(iend - anchor);
  if (((byte*)op - dest) + lastRun + 1 + ((lastRun-15)/255) >= maxOutputSize) return 0;
                    if (lastRun >= (int)LZ4Util.RUN_MASK) { *op++ = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); lastRun -= (byte)LZ4Util.RUN_MASK; for (; lastRun > 254; lastRun -= 255) *op++ = 255; *op++ = (byte)lastRun; }
                    else *op++ = (byte)(lastRun << LZ4Util.ML_BITS);
                    LZ4Util.CopyMemory(op, anchor, iend - anchor);
                    op += iend - anchor;
                }

                // End
                return (int)(((byte*)op) - dest);
            }
        }




        // Note : this function is valid only if isize < LZ4_64KLIMIT







        int Compress64K(byte* source, byte* dest, int isize, int maxOutputSize)
        {
            fixed (byte* hashTablePtr = m_HashTable)
            fixed (byte* deBruijnBytePos = DeBruijnBytePos)
            {
                Clear(hashTablePtr, sizeof(ushort) * HASHTABLESIZE * 2);
                ushort* hashTable = (ushort*)hashTablePtr;

                byte* ip = (byte*)source;
                byte* anchor = ip;
                byte* basep = ip;
                byte* iend = ip + isize;
                byte* mflimit = iend - MFLIMIT;
                byte* matchlimit = (iend - LASTLITERALS);
                byte* op = (byte*)dest;
    byte* oend = dest + maxOutputSize;

                int len, length;
                const int skipStrength = SKIPSTRENGTH;
                uint forwardH;

                // Init
                if (isize < MINLENGTH) goto _last_literals;

                // First Byte
                ip++; forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)));

                // Main Loop
                for (; ; )
                {
                    int findMatchAttempts = (int)(1U << skipStrength) + 3;
                    byte* forwardIp = ip;
                    byte* r;
                    byte* token;

                    // Find a match
                    do
                    {
                        uint h = forwardH;
                        int step = findMatchAttempts++ >> skipStrength;
                        ip = forwardIp;
                        forwardIp = ip + step;

                        if (forwardIp > mflimit) { goto _last_literals; }

                        forwardH = (((*(uint*)forwardIp) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)));
                        r = basep + hashTable[h];
                        hashTable[h] = (ushort)(ip - basep);

                    } while (*(uint*)r != *(uint*)ip);

                    // Catch up
                    while ((ip > anchor) && (r > (byte*)source) && (ip[-1] == r[-1])) { ip--; r--; }

                    // Encode Literal Length
                    length = (int)(ip - anchor);
                    token = op++;
  if (op + length + (2 + 1 + LASTLITERALS) + (length>>8) >= oend) return 0; // Check output limit
                    if (length >= (int)LZ4Util.RUN_MASK) { *token = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); len = (int)(length - LZ4Util.RUN_MASK); for (; len > 254; len -= 255) *op++ = 255; *op++ = (byte)len; }
                    else *token = (byte)(length << LZ4Util.ML_BITS);

                    // Copy Literals
                    { byte* e=(op)+length; do { *(uint*)op = *(uint*)anchor; op+=4; anchor+=4;; *(uint*)op = *(uint*)anchor; op+=4; anchor+=4;; } while (op<e);; op=e; };


                _next_match:
                    // Encode Offset
                    *(ushort*)op = (ushort)(ip - r); op += 2;

                    // Start Counting
                    ip += MINMATCH; r += MINMATCH; // MinMatch verified
                    anchor = ip;
                    //					while (ip<matchlimit-3)
                    //					{
                    //						if (*(uint *)r == *(uint *)ip) { ip+=4; r+=4; continue; }
                    //						if (*(ushort *)r == *(ushort *)ip) { ip+=2; r+=2; }
                    //						if (*r == *ip) ip++;

                    while (ip < matchlimit - (STEPSIZE-1))
                    {
                        int diff = (int)(*(int*)(r) ^ *(int*)(ip));
                        if (diff == 0) { ip += STEPSIZE; r += STEPSIZE; continue; }
                        ip += DeBruijnBytePos[((uint)((diff & -diff) * 0x077CB531U)) >> 27];;
                        goto _endCount;
                    }



                    if ((ip < (matchlimit - 1)) && (*(ushort*)r == *(ushort*)ip)) { ip += 2; r += 2; }
                    if ((ip < matchlimit) && (*r == *ip)) ip++;
                _endCount:
                    len = (int)(ip - anchor);

                    //Encode MatchLength
                    if (len >= (int)LZ4Util.ML_MASK) { *token = (byte)(*token + LZ4Util.ML_MASK); len = (int)(len - LZ4Util.ML_MASK); for (; len > 509; len -= 510) { *op++ = 255; *op++ = 255; } if (len > 254) { len -= 255; *op++ = 255; } *op++ = (byte)len; }
                    else *token = (byte)(*token + len);

                    // Test end of chunk
                    if (ip > mflimit) { anchor = ip; break; }

                    // Fill table
              hashTable[(((*(uint*)ip-2) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)))] = (ushort)(ip - 2 - basep);

                    // Test next position
                    r = basep + hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)))];
                    hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)))] = (ushort)(ip - basep);
                    if (*(uint*)r == *(uint*)ip) { token = op++; *token = 0; goto _next_match; }

                    // Prepare next loop
                    anchor = ip++;
                    forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)));
                }

            _last_literals:
                {
                    int lastRun = (int)(iend - anchor);
  if (((byte*)op - dest) + lastRun + 1 + ((lastRun)>>8) >= maxOutputSize) return 0;
                    if (lastRun >= (int)LZ4Util.RUN_MASK) { *op++ = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); lastRun -= (byte)LZ4Util.RUN_MASK; for (; lastRun > 254; lastRun -= 255) *op++ = 255; *op++ = (byte)lastRun; }
                    else *op++ = (byte)(lastRun << LZ4Util.ML_BITS);
                    LZ4Util.CopyMemory(op, anchor, iend - anchor);
                    op += iend - anchor;
                }


                return (int)(((byte*)op) - dest);
            }
        }

        /// <summary>
        /// TODO: test if this is faster or slower than Array.Clear.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="count"></param>
        static void Clear(byte* ptr, int count)
        {
            long* p = (long*)ptr;
            int longCount = count >> SIZE_OF_LONG_TIMES_TWO_SHIFT; // count / sizeof(long) * 2;
            while (longCount-- != 0)
            {
                *p++ = 0L;
                *p++ = 0L;
            }


            Debug.Assert(count % 16 == 0, "HashTable size must be divisible by 16");

            //for (int i = longCount << 4 ; i < count; i++)
            //    ptr[i] = 0;

        }
    }


    /// <summary>
    /// Class for compressing a byte array into an LZ4 byte array.
    /// </summary>
 public unsafe class LZ4Compressor64 : ILZ4Compressor
    {
        //**************************************
        // Tuning parameters
        //**************************************
        // COMPRESSIONLEVEL :
        // Increasing this value improves compression ratio
        // Lowering this value reduces memory usage
        // Reduced memory usage typically improves speed, due to cache effect (ex : L1 32KB for Intel, L1 64KB for AMD)
        // Memory usage formula : N->2^(N+2) Bytes (examples : 12 -> 16KB ; 17 -> 512KB)
        const int COMPRESSIONLEVEL = 12;

        // NOTCOMPRESSIBLE_CONFIRMATION :
        // Decreasing this value will make the algorithm skip faster data segments considered "incompressible"
        // This may decrease compression ratio dramatically, but will be faster on incompressible data
        // Increasing this value will make the algorithm search more before declaring a segment "incompressible"
        // This could improve compression a bit, but will be slower on incompressible data
        // The default value (6) is recommended
        // 2 is the minimum value.
        const int NOTCOMPRESSIBLE_CONFIRMATION = 6;

        //**************************************
        // Constants
        //**************************************
        const int HASH_LOG = COMPRESSIONLEVEL;
        const int MAXD_LOG = 16;
        const int MAX_DISTANCE = ((1 << MAXD_LOG) - 1);
        const int MINMATCH = 4;
        const int MFLIMIT = (LZ4Util.COPYLENGTH + MINMATCH);
        const int MINLENGTH = (MFLIMIT + 1);
        const uint LZ4_64KLIMIT = ((1U << 16) + (MFLIMIT - 1));
        const int HASHLOG64K = (HASH_LOG + 1);
        const int HASHTABLESIZE = (1 << HASH_LOG);
        const int HASH_MASK = (HASHTABLESIZE - 1);
        const int LASTLITERALS = 5;
        const int SKIPSTRENGTH = (NOTCOMPRESSIBLE_CONFIRMATION > 2 ? NOTCOMPRESSIBLE_CONFIRMATION : 2);
        const int SIZE_OF_LONG_TIMES_TWO_SHIFT = 4;
        const int STEPSIZE = 8;
        static byte[] DeBruijnBytePos = new byte[64]{ 0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7, 0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7, 7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6, 7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7 };
        //**************************************
        // Macros
        //**************************************
        byte[] m_HashTable;

        public LZ4Compressor64()
        {
            m_HashTable = new byte[HASHTABLESIZE * IntPtr.Size];
            if (m_HashTable.Length % 16 != 0)
                throw new Exception("Hash table size must be divisible by 16");
        }


        public byte[] Compress(byte[] source)
        {
            int maxCompressedSize = CalculateMaxCompressedLength(source.Length);
            byte[] dst = new byte[maxCompressedSize];
            int length = Compress(source, dst);
            byte[] dest = new byte[length];
            Buffer.BlockCopy(dst, 0, dest, 0, length);
            return dest;
        }

        /// <summary>
        /// Calculate the max compressed byte[] size given the size of the uncompressed byte[]
        /// </summary>
        /// <param name="uncompressedLength">Length of the uncompressed data</param>
        /// <returns>The maximum required size in bytes of the compressed data</returns>
        public int CalculateMaxCompressedLength(int uncompressedLength)
        {
   return uncompressedLength + (uncompressedLength / 255) + 16;
        }

        /// <summary>
        /// Compress source into dest returning compressed length
        /// </summary>
        /// <param name="source">uncompressed data</param>
        /// <param name="dest">array into which source will be compressed</param>
        /// <returns>compressed length</returns>
        public int Compress(byte[] source, byte[] dest)
        {
            fixed (byte* s = source)
            fixed (byte* d = dest)
            {
                if (source.Length < (int)LZ4_64KLIMIT)
                    return Compress64K(s, d, source.Length, dest.Length);
                return Compress(s, d, source.Length, dest.Length);
            }
        }

        /// <summary>
        /// Compress source into dest returning compressed length
        /// </summary>
        /// <param name="source">uncompressed data</param>
        /// <param name="srcOffset">offset in source array where reading will start</param>
        /// <param name="count">count of bytes in source array to compress</param>
        /// <param name="dest">array into which source will be compressed</param>
        /// <param name="dstOffset">start index in dest array where writing will start</param>
        /// <returns>compressed length</returns>
        public int Compress(byte[] source, int srcOffset, int count, byte[] dest, int dstOffset)
        {
            fixed (byte* s = &source[srcOffset])
            fixed (byte* d = &dest[dstOffset])
            {
                if (source.Length < (int)LZ4_64KLIMIT)
                    return Compress64K(s, d, count, dest.Length - dstOffset);
                return Compress(s, d, count, dest.Length - dstOffset);
            }
        }

        int Compress(byte* source, byte* dest, int isize, int maxOutputSize)
        {
            fixed (byte* hashTablePtr = m_HashTable)
            fixed (byte* deBruijnBytePos = DeBruijnBytePos)
            {
                Clear(hashTablePtr, sizeof(byte*) * HASHTABLESIZE);
                byte** hashTable = (byte**)hashTablePtr;

                byte* ip = (byte*)source;
                long basePtr = (long)ip;

                byte* anchor = ip;
                byte* iend = ip + isize;
                byte* mflimit = iend - MFLIMIT;
                byte* matchlimit = (iend - LASTLITERALS);
    byte* oend = dest + maxOutputSize;


                byte* op = (byte*)dest;

                int len, length;
                const int skipStrength = SKIPSTRENGTH;
                uint forwardH;


                // Init
                if (isize < MINLENGTH) goto _last_literals;

                // First Byte
                hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))] = ip - basePtr;
                ip++; forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG));

                // Main Loop
                for (; ; )
                {
                    uint findMatchAttempts = (1U << skipStrength) + 3;
                    byte* forwardIp = ip;
                    byte* r;
                    byte* token;

                    // Find a match
                    do
                    {
                        uint h = forwardH;
                        uint step = findMatchAttempts++ >> skipStrength;
                        ip = forwardIp;
                        forwardIp = ip + step;

                        if (forwardIp > mflimit) { goto _last_literals; }

                        // LZ4_HASH_VALUE
                        forwardH = (((*(uint*)forwardIp) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG));
                        r = hashTable[h] + basePtr;
                        hashTable[h] = ip - basePtr;

                    } while ((r < ip - MAX_DISTANCE) || (*(uint*)r != *(uint*)ip));

                    // Catch up
                    while ((ip > anchor) && (r > (byte*)source) && (ip[-1] == r[-1])) { ip--; r--; }

                    // Encode Literal Length
                    length = (int)(ip - anchor);
                    token = op++;
                    if (length >= (int)LZ4Util.RUN_MASK) { *token = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); len = (int)(length - LZ4Util.RUN_MASK); for (; len > 254; len -= 255) *op++ = 255; *op++ = (byte)len; }
                    else *token = (byte)(length << LZ4Util.ML_BITS);

                    //Copy Literals
                    { byte* e=(op)+length; do { *(ulong*)op = *(ulong*)anchor; op+=8; anchor+=8; } while (op<e);; op=e; };

                _next_match:
                    // Encode Offset
                    *(ushort*)op = (ushort)(ip - r); op += 2;

                    // Start Counting
                    ip += MINMATCH; r += MINMATCH; // MinMatch verified
                    anchor = ip;
                    //					while (*(uint *)r == *(uint *)ip)
                    //					{
                    //						ip+=4; r+=4;
                    //						if (ip>matchlimit-4) { r -= ip - (matchlimit-3); ip = matchlimit-3; break; }
                    //					}
                    //					if (*(ushort *)r == *(ushort *)ip) { ip+=2; r+=2; }
                    //					if (*r == *ip) ip++;

                    while (ip < matchlimit - (STEPSIZE -1))
                    {
                        long diff = (long)(*(long*)(r) ^ *(long*)(ip));
                        if (diff == 0) { ip += STEPSIZE; r += STEPSIZE; continue; }
                        ip += DeBruijnBytePos[((ulong)((diff & -diff) * 0x0218A392CDABBD3F)) >> 58];;
                        goto _endCount;
                    }

                    if ((ip<(matchlimit-3)) && (*(uint*)r == *(uint*)ip)) { ip+=4; r+=4; }


                    if ((ip < (matchlimit - 1)) && (*(ushort*)(r) == *(ushort*)(ip))) { ip += 2; r += 2; }
                    if ((ip < matchlimit) && (*r == *ip)) ip++;
                _endCount:

                    len = (int)(ip - anchor);
  if (op + (1 + LASTLITERALS) + (len>>8) >= oend) return 0; // Check output limit
                    // Encode MatchLength
                    if (len >= (int)LZ4Util.ML_MASK) { *token += (byte)LZ4Util.ML_MASK; len -= (byte)LZ4Util.ML_MASK; for (; len > 509; len -= 510) { *op++ = 255; *op++ = 255; } if (len > 254) { len -= 255; *op++ = 255; } *op++ = (byte)len; }
                    else *token += (byte)len;

                    // Test end of chunk
                    if (ip > mflimit) { anchor = ip; break; }

                    // Fill table
                    hashTable[(((*(uint*)ip-2) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))] = ip - 2 - basePtr;

                    // Test next position
                    r = basePtr + hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))];
                    hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG))] = ip - basePtr;
                    if ((r > ip - (MAX_DISTANCE + 1)) && (*(uint*)r == *(uint*)ip)) { token = op++; *token = 0; goto _next_match; }

                    // Prepare next loop
                    anchor = ip++;
                    forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-HASH_LOG));
                }

            _last_literals:
                // Encode Last Literals
                {
                    int lastRun = (int)(iend - anchor);
  if (((byte*)op - dest) + lastRun + 1 + ((lastRun-15)/255) >= maxOutputSize) return 0;
                    if (lastRun >= (int)LZ4Util.RUN_MASK) { *op++ = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); lastRun -= (byte)LZ4Util.RUN_MASK; for (; lastRun > 254; lastRun -= 255) *op++ = 255; *op++ = (byte)lastRun; }
                    else *op++ = (byte)(lastRun << LZ4Util.ML_BITS);
                    LZ4Util.CopyMemory(op, anchor, iend - anchor);
                    op += iend - anchor;
                }

                // End
                return (int)(((byte*)op) - dest);
            }
        }




        // Note : this function is valid only if isize < LZ4_64KLIMIT







        int Compress64K(byte* source, byte* dest, int isize, int maxOutputSize)
        {
            fixed (byte* hashTablePtr = m_HashTable)
            fixed (byte* deBruijnBytePos = DeBruijnBytePos)
            {
                Clear(hashTablePtr, sizeof(ushort) * HASHTABLESIZE * 2);
                ushort* hashTable = (ushort*)hashTablePtr;

                byte* ip = (byte*)source;
                byte* anchor = ip;
                byte* basep = ip;
                byte* iend = ip + isize;
                byte* mflimit = iend - MFLIMIT;
                byte* matchlimit = (iend - LASTLITERALS);
                byte* op = (byte*)dest;
    byte* oend = dest + maxOutputSize;

                int len, length;
                const int skipStrength = SKIPSTRENGTH;
                uint forwardH;

                // Init
                if (isize < MINLENGTH) goto _last_literals;

                // First Byte
                ip++; forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)));

                // Main Loop
                for (; ; )
                {
                    int findMatchAttempts = (int)(1U << skipStrength) + 3;
                    byte* forwardIp = ip;
                    byte* r;
                    byte* token;

                    // Find a match
                    do
                    {
                        uint h = forwardH;
                        int step = findMatchAttempts++ >> skipStrength;
                        ip = forwardIp;
                        forwardIp = ip + step;

                        if (forwardIp > mflimit) { goto _last_literals; }

                        forwardH = (((*(uint*)forwardIp) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)));
                        r = basep + hashTable[h];
                        hashTable[h] = (ushort)(ip - basep);

                    } while (*(uint*)r != *(uint*)ip);

                    // Catch up
                    while ((ip > anchor) && (r > (byte*)source) && (ip[-1] == r[-1])) { ip--; r--; }

                    // Encode Literal Length
                    length = (int)(ip - anchor);
                    token = op++;
  if (op + length + (2 + 1 + LASTLITERALS) + (length>>8) >= oend) return 0; // Check output limit
                    if (length >= (int)LZ4Util.RUN_MASK) { *token = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); len = (int)(length - LZ4Util.RUN_MASK); for (; len > 254; len -= 255) *op++ = 255; *op++ = (byte)len; }
                    else *token = (byte)(length << LZ4Util.ML_BITS);

                    // Copy Literals
                    { byte* e=(op)+length; do { *(ulong*)op = *(ulong*)anchor; op+=8; anchor+=8; } while (op<e);; op=e; };


                _next_match:
                    // Encode Offset
                    *(ushort*)op = (ushort)(ip - r); op += 2;

                    // Start Counting
                    ip += MINMATCH; r += MINMATCH; // MinMatch verified
                    anchor = ip;
                    //					while (ip<matchlimit-3)
                    //					{
                    //						if (*(uint *)r == *(uint *)ip) { ip+=4; r+=4; continue; }
                    //						if (*(ushort *)r == *(ushort *)ip) { ip+=2; r+=2; }
                    //						if (*r == *ip) ip++;

                    while (ip < matchlimit - (STEPSIZE-1))
                    {
                        long diff = (long)(*(long*)(r) ^ *(long*)(ip));
                        if (diff == 0) { ip += STEPSIZE; r += STEPSIZE; continue; }
                        ip += DeBruijnBytePos[((ulong)((diff & -diff) * 0x0218A392CDABBD3F)) >> 58];;
                        goto _endCount;
                    }

                    if ((ip<(matchlimit-3)) && (*(uint*)r == *(uint*)ip)) { ip+=4; r+=4; }

                    if ((ip < (matchlimit - 1)) && (*(ushort*)r == *(ushort*)ip)) { ip += 2; r += 2; }
                    if ((ip < matchlimit) && (*r == *ip)) ip++;
                _endCount:
                    len = (int)(ip - anchor);

                    //Encode MatchLength
                    if (len >= (int)LZ4Util.ML_MASK) { *token = (byte)(*token + LZ4Util.ML_MASK); len = (int)(len - LZ4Util.ML_MASK); for (; len > 509; len -= 510) { *op++ = 255; *op++ = 255; } if (len > 254) { len -= 255; *op++ = 255; } *op++ = (byte)len; }
                    else *token = (byte)(*token + len);

                    // Test end of chunk
                    if (ip > mflimit) { anchor = ip; break; }

                    // Fill table
              hashTable[(((*(uint*)ip-2) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)))] = (ushort)(ip - 2 - basep);

                    // Test next position
                    r = basep + hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)))];
                    hashTable[(((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)))] = (ushort)(ip - basep);
                    if (*(uint*)r == *(uint*)ip) { token = op++; *token = 0; goto _next_match; }

                    // Prepare next loop
                    anchor = ip++;
                    forwardH = (((*(uint*)ip) * 2654435761U) >> ((MINMATCH*8)-(HASH_LOG+1)));
                }

            _last_literals:
                {
                    int lastRun = (int)(iend - anchor);
  if (((byte*)op - dest) + lastRun + 1 + ((lastRun)>>8) >= maxOutputSize) return 0;
                    if (lastRun >= (int)LZ4Util.RUN_MASK) { *op++ = (byte)(LZ4Util.RUN_MASK << LZ4Util.ML_BITS); lastRun -= (byte)LZ4Util.RUN_MASK; for (; lastRun > 254; lastRun -= 255) *op++ = 255; *op++ = (byte)lastRun; }
                    else *op++ = (byte)(lastRun << LZ4Util.ML_BITS);
                    LZ4Util.CopyMemory(op, anchor, iend - anchor);
                    op += iend - anchor;
                }


                return (int)(((byte*)op) - dest);
            }
        }

        /// <summary>
        /// TODO: test if this is faster or slower than Array.Clear.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="count"></param>
        static void Clear(byte* ptr, int count)
        {
            long* p = (long*)ptr;
            int longCount = count >> SIZE_OF_LONG_TIMES_TWO_SHIFT; // count / sizeof(long) * 2;
            while (longCount-- != 0)
            {
                *p++ = 0L;
                *p++ = 0L;
            }


            Debug.Assert(count % 16 == 0, "HashTable size must be divisible by 16");

            //for (int i = longCount << 4 ; i < count; i++)
            //    ptr[i] = 0;

        }
    }

    public static class LZ4CompressorFactory
    {
        public static ILZ4Compressor CreateNew()
        {
            if (IntPtr.Size == 4)
                return new LZ4Compressor32();
            return new LZ4Compressor64();
        }
    }


    /// <summary>
    /// Class for decompressing an LZ4 compressed byte array.
    /// </summary>
     public unsafe class LZ4Decompressor32 : ILZ4Decompressor
    {
        const int STEPSIZE = 4;





        static byte[] DeBruijnBytePos = new byte[32] { 0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1, 3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1 };
        //**************************************
        // Macros
        //**************************************
        readonly sbyte[] m_DecArray = new sbyte[8] { 0, 3, 2, 3,0,0,0,0 };
        // Note : The decoding functions LZ4_uncompress() and LZ4_uncompress_unknownOutputSize()
        //              are safe against "buffer overflow" attack type
        //              since they will *never* write outside of the provided output buffer :
        //              they both check this condition *before* writing anything.
        //              A corrupted packet however can make them *read* within the first 64K before the output buffer.
        /// <summary>
        /// Decompress.
        /// </summary>
        /// <param name="source">compressed array</param>
        /// <param name="dest">This must be the exact length of the decompressed item</param>
        public void DecompressKnownSize(byte[] compressed, byte[] decompressed)
        {
            int len = DecompressKnownSize(compressed, decompressed, decompressed.Length);
            Debug.Assert(len == decompressed.Length);
        }
        public int DecompressKnownSize(byte[] compressed, byte[] decompressedBuffer, int decompressedSize)
        {
            fixed (byte* src = compressed)
            fixed (byte* dst = decompressedBuffer)
                return DecompressKnownSize(src, dst, decompressedSize);
        }
        public int DecompressKnownSize(byte* compressed, byte* decompressedBuffer, int decompressedSize)
        {
            fixed (sbyte* dec = m_DecArray)


            {
                // Local Variables
                byte* ip = (byte*)compressed;
                byte* r;

                byte* op = (byte*)decompressedBuffer;
                byte* oend = op + decompressedSize;
                byte* cpy;

                byte token;
                int len, length;


                // Main Loop
                while (true)
                {
                    // get runLength
                    token = *ip++;
                    if ((length = (token >> LZ4Util.ML_BITS)) == LZ4Util.RUN_MASK) { for (; (len = *ip++) == 255; length += 255) { } length += len; }


                    cpy = op + length;
                    if (cpy > oend - LZ4Util.COPYLENGTH)
                    {
                        if (cpy > oend) goto _output_error;
                        LZ4Util.CopyMemory(op, ip, length);
                        ip += length;
                        break;
                    }

                    do { *(uint*)op = *(uint*)ip; op+=4; ip+=4;; *(uint*)op = *(uint*)ip; op+=4; ip+=4;; } while (op<cpy);; ip -= (op - cpy); op = cpy;


                    // get offset
                    { r = (cpy) - *(ushort*)ip; }; ip+=2;
     if(r < decompressedBuffer) goto _output_error;

                    // get matchLength
                    if ((length = (int)(token & LZ4Util.ML_MASK)) == LZ4Util.ML_MASK) { for (; *ip == 255; length += 255) { ip++; } length += *ip++; }

                    // copy repeated sequence
                    if (op - r < STEPSIZE)
                    {



                        const int dec2 = 0;



                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        r -= dec[op - r];
                        *(uint*)op = *(uint*)r; op += STEPSIZE - 4;
                        r -= dec2;
                    }
                    else { *(uint*)op = *(uint*)r; op+=4; r+=4;; }
                    cpy = op + length - (STEPSIZE - 4);
                    if (cpy > oend - LZ4Util.COPYLENGTH)
                    {
                        if (cpy > oend) goto _output_error;

                        do { *(uint*)op = *(uint*)r; op+=4; r+=4;; *(uint*)op = *(uint*)r; op+=4; r+=4;; } while (op<(oend - LZ4Util.COPYLENGTH));;
                        while (op < cpy) *op++ = *r++;
                        op = cpy;
                        if (op == oend) break;
                        continue;
                    }

                    do { *(uint*)op = *(uint*)r; op+=4; r+=4;; *(uint*)op = *(uint*)r; op+=4; r+=4;; } while (op<cpy);;
                    op = cpy; // correction
                }

                // end of decoding
                return (int)(((byte*)ip) - compressed);

                // write overflow error detected
            _output_error:
                return (int)(-(((byte*)ip) - compressed));
            }
        }

        public byte[] Decompress(byte[] compressed)
        {
   int length = compressed.Length;
            int len;
            byte[] dest;
   const int Multiplier = 4; // Just a number. Determines how fast length should increase.
            do
            {
                length *= Multiplier;
                dest = new byte[length];
                len = Decompress(compressed, dest, compressed.Length);
            }
            while (len < 0 || dest.Length < len);

            byte[] d = new byte[len];
            Buffer.BlockCopy(dest, 0, d, 0, d.Length);
            return d;
        }

        public int Decompress(byte[] compressed, byte[] decompressedBuffer)
        {
            return Decompress(compressed, decompressedBuffer, compressed.Length);
        }

        public int Decompress(byte[] compressedBuffer, byte[] decompressedBuffer, int compressedSize)
        {
            fixed (byte* src = compressedBuffer)
            fixed (byte* dst = decompressedBuffer)
                return Decompress(src, dst, compressedSize, decompressedBuffer.Length);
        }

        public int Decompress(byte[] compressedBuffer, int compressedPosition, byte[] decompressedBuffer, int decompressedPosition, int compressedSize)
        {
            fixed (byte* src = &compressedBuffer[compressedPosition])
            fixed (byte* dst = &decompressedBuffer[decompressedPosition])
                return Decompress(src, dst, compressedSize, decompressedBuffer.Length);
        }

        public int Decompress(
            byte* compressedBuffer,
            byte* decompressedBuffer,
            int compressedSize,
            int maxDecompressedSize)
        {
            fixed (sbyte* dec = m_DecArray)



            {
                // Local Variables
                byte* ip = (byte*)compressedBuffer;
                byte* iend = ip + compressedSize;
                byte* r;

                byte* op = (byte*)decompressedBuffer;
                byte* oend = op + maxDecompressedSize;
                byte* cpy;

                byte token;
                int len, length;


                // Main Loop
                while (ip < iend)
                {
                    // get runLength
                    token = *ip++;
  if ((length=(token>>LZ4Util.ML_BITS)) == LZ4Util.RUN_MASK) { int s=255; while ((ip<iend) && (s==255)) { s=*ip++; length += s; } }

                    // copy literals
                    cpy = op + length;
  if ((cpy>oend-LZ4Util.COPYLENGTH) || (ip+length>iend-LZ4Util.COPYLENGTH))
                    {
   if (cpy > oend) goto _output_error; // Error : request to write beyond destination buffer
   if (ip+length > iend) goto _output_error; // Error : request to read beyond source buffer
                        LZ4Util.CopyMemory(op, ip, length);
                        op += length;
   ip += length;
   if (ip<iend) goto _output_error; // Error : LZ4 format violation
                        break; //Necessarily EOF
                    }

                    do { *(uint*)op = *(uint*)ip; op+=4; ip+=4;; *(uint*)op = *(uint*)ip; op+=4; ip+=4;; } while (op<cpy);; ip -= (op - cpy); op = cpy;

                    // get offset
                    { r = (cpy) - *(ushort*)ip; }; ip+=2;
                    if (r < decompressedBuffer) goto _output_error;

                    // get matchlength
  if ((length=(int)(token&LZ4Util.ML_MASK)) == LZ4Util.ML_MASK) { while (ip<iend) { int s = *ip++; length +=s; if (s==255) continue; break; } }

                    // copy repeated sequence
                    if (op - r < STEPSIZE)
                    {



               const int dec2 = 0;


                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        r -= dec[op - r];
                        *(uint*)op = *(uint*)r; op += STEPSIZE - 4;
                        r -= dec2;
                    }
                    else { *(uint*)op = *(uint*)r; op+=4; r+=4;; }
                    cpy = op + length - (STEPSIZE - 4);
                    if (cpy > oend - LZ4Util.COPYLENGTH)
                    {
                        if (cpy > oend) goto _output_error;

                        do { *(uint*)op = *(uint*)r; op+=4; r+=4;; *(uint*)op = *(uint*)r; op+=4; r+=4;; } while (op<(oend - LZ4Util.COPYLENGTH));;
                        while (op < cpy) *op++ = *r++;
                        op = cpy;
                        if (op == oend) break; // Check EOF (should never happen, since last 5 bytes are supposed to be literals)
                        continue;
                    }
                    do { *(uint*)op = *(uint*)r; op+=4; r+=4;; *(uint*)op = *(uint*)r; op+=4; r+=4;; } while (op<cpy);;
                    op = cpy; // correction
                }


                return (int)(((byte*)op) - decompressedBuffer);


            _output_error:
                return (int)(-(((byte*)ip) - compressedBuffer));
            }
        }
    }


    /// <summary>
    /// Class for decompressing an LZ4 compressed byte array.
    /// </summary>
     public unsafe class LZ4Decompressor64 : ILZ4Decompressor
    {
        const int STEPSIZE = 8;
        static byte[] DeBruijnBytePos = new byte[64]{ 0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7, 0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7, 7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6, 7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7 };
        //**************************************
        // Macros
        //**************************************
        readonly sbyte[] m_DecArray = new sbyte[8] { 0, 3, 2, 3,0,0,0,0 };
  readonly sbyte[] m_Dec2table= new sbyte[8]{0, 0, 0, -1, 0, 1, 2, 3};
        // Note : The decoding functions LZ4_uncompress() and LZ4_uncompress_unknownOutputSize()
        //              are safe against "buffer overflow" attack type
        //              since they will *never* write outside of the provided output buffer :
        //              they both check this condition *before* writing anything.
        //              A corrupted packet however can make them *read* within the first 64K before the output buffer.

        /// <summary>
        /// Decompress.
        /// </summary>
        /// <param name="source">compressed array</param>
        /// <param name="dest">This must be the exact length of the decompressed item</param>
        public void DecompressKnownSize(byte[] compressed, byte[] decompressed)
        {
            int len = DecompressKnownSize(compressed, decompressed, decompressed.Length);
            Debug.Assert(len == decompressed.Length);
        }

        public int DecompressKnownSize(byte[] compressed, byte[] decompressedBuffer, int decompressedSize)
        {
            fixed (byte* src = compressed)
            fixed (byte* dst = decompressedBuffer)
                return DecompressKnownSize(src, dst, decompressedSize);
        }

        public int DecompressKnownSize(byte* compressed, byte* decompressedBuffer, int decompressedSize)
        {
            fixed (sbyte* dec = m_DecArray)

            fixed(sbyte* dec2Ptr = m_Dec2table)

            {
                // Local Variables
                byte* ip = (byte*)compressed;
                byte* r;

                byte* op = (byte*)decompressedBuffer;
                byte* oend = op + decompressedSize;
                byte* cpy;

                byte token;
                int len, length;


                // Main Loop
                while (true)
                {
                    // get runLength
                    token = *ip++;
                    if ((length = (token >> LZ4Util.ML_BITS)) == LZ4Util.RUN_MASK) { for (; (len = *ip++) == 255; length += 255) { } length += len; }


                    cpy = op + length;
                    if (cpy > oend - LZ4Util.COPYLENGTH)
                    {
                        if (cpy > oend) goto _output_error;
                        LZ4Util.CopyMemory(op, ip, length);
                        ip += length;
                        break;
                    }

                    do { *(ulong*)op = *(ulong*)ip; op+=8; ip+=8; } while (op<cpy);; ip -= (op - cpy); op = cpy;


                    // get offset
                    { r = (cpy) - *(ushort*)ip; }; ip+=2;
     if(r < decompressedBuffer) goto _output_error;

                    // get matchLength
                    if ((length = (int)(token & LZ4Util.ML_MASK)) == LZ4Util.ML_MASK) { for (; *ip == 255; length += 255) { ip++; } length += *ip++; }

                    // copy repeated sequence
                    if (op - r < STEPSIZE)
                    {

                        var dec2 = dec2Ptr[(int)(op-r)];





                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        r -= dec[op - r];
                        *(uint*)op = *(uint*)r; op += STEPSIZE - 4;
                        r -= dec2;
                    }
                    else { *(ulong*)op = *(ulong*)r; op+=8; r+=8;; }
                    cpy = op + length - (STEPSIZE - 4);
                    if (cpy > oend - LZ4Util.COPYLENGTH)
                    {
                        if (cpy > oend) goto _output_error;

                        if (op<(oend - LZ4Util.COPYLENGTH)) do { *(ulong*)op = *(ulong*)r; op+=8; r+=8; } while (op<(oend - LZ4Util.COPYLENGTH));;
                        while (op < cpy) *op++ = *r++;
                        op = cpy;
                        if (op == oend) break;
                        continue;
                    }

                    if (op<cpy) do { *(ulong*)op = *(ulong*)r; op+=8; r+=8; } while (op<cpy);;
                    op = cpy; // correction
                }

                // end of decoding
                return (int)(((byte*)ip) - compressed);

                // write overflow error detected
            _output_error:
                return (int)(-(((byte*)ip) - compressed));
            }
        }

        public byte[] Decompress(byte[] compressed)
        {
   int length = compressed.Length;
            int len;
            byte[] dest;
   const int Multiplier = 4; // Just a number. Determines how fast length should increase.
            do
            {
                length *= Multiplier;
                dest = new byte[length];
                len = Decompress(compressed, dest, compressed.Length);
            }
            while (len < 0 || dest.Length < len);

            byte[] d = new byte[len];
            Buffer.BlockCopy(dest, 0, d, 0, d.Length);
            return d;
        }

        public int Decompress(byte[] compressed, byte[] decompressedBuffer)
        {
            return Decompress(compressed, decompressedBuffer, compressed.Length);
        }

        public int Decompress(byte[] compressedBuffer, byte[] decompressedBuffer, int compressedSize)
        {
            fixed (byte* src = compressedBuffer)
            fixed (byte* dst = decompressedBuffer)
                return Decompress(src, dst, compressedSize, decompressedBuffer.Length);
        }

        public int Decompress(byte[] compressedBuffer, int compressedPosition, byte[] decompressedBuffer, int decompressedPosition, int compressedSize)
        {
            fixed (byte* src = &compressedBuffer[compressedPosition])
            fixed (byte* dst = &decompressedBuffer[decompressedPosition])
                return Decompress(src, dst, compressedSize, decompressedBuffer.Length);
        }

        public int Decompress(
            byte* compressedBuffer,
            byte* decompressedBuffer,
            int compressedSize,
            int maxDecompressedSize)
        {
            fixed (sbyte* dec = m_DecArray)

            fixed(sbyte* dec2Ptr = m_Dec2table)

            {
                // Local Variables
                byte* ip = (byte*)compressedBuffer;
                byte* iend = ip + compressedSize;
                byte* r;

                byte* op = (byte*)decompressedBuffer;
                byte* oend = op + maxDecompressedSize;
                byte* cpy;

                byte token;
                int len, length;


                // Main Loop
                while (ip < iend)
                {
                    // get runLength
                    token = *ip++;
  if ((length=(token>>LZ4Util.ML_BITS)) == LZ4Util.RUN_MASK) { int s=255; while ((ip<iend) && (s==255)) { s=*ip++; length += s; } }

                    // copy literals
                    cpy = op + length;
  if ((cpy>oend-LZ4Util.COPYLENGTH) || (ip+length>iend-LZ4Util.COPYLENGTH))
                    {
   if (cpy > oend) goto _output_error; // Error : request to write beyond destination buffer
   if (ip+length > iend) goto _output_error; // Error : request to read beyond source buffer
                        LZ4Util.CopyMemory(op, ip, length);
                        op += length;
   ip += length;
   if (ip<iend) goto _output_error; // Error : LZ4 format violation
                        break; //Necessarily EOF
                    }

                    do { *(ulong*)op = *(ulong*)ip; op+=8; ip+=8; } while (op<cpy);; ip -= (op - cpy); op = cpy;

                    // get offset
                    { r = (cpy) - *(ushort*)ip; }; ip+=2;
                    if (r < decompressedBuffer) goto _output_error;

                    // get matchlength
  if ((length=(int)(token&LZ4Util.ML_MASK)) == LZ4Util.ML_MASK) { while (ip<iend) { int s = *ip++; length +=s; if (s==255) continue; break; } }

                    // copy repeated sequence
                    if (op - r < STEPSIZE)
                    {

                        var dec2 = dec2Ptr[op-r];




                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        *op++ = *r++;
                        r -= dec[op - r];
                        *(uint*)op = *(uint*)r; op += STEPSIZE - 4;
                        r -= dec2;
                    }
                    else { *(ulong*)op = *(ulong*)r; op+=8; r+=8;; }
                    cpy = op + length - (STEPSIZE - 4);
                    if (cpy > oend - LZ4Util.COPYLENGTH)
                    {
                        if (cpy > oend) goto _output_error;

                        if (op<(oend - LZ4Util.COPYLENGTH)) do { *(ulong*)op = *(ulong*)r; op+=8; r+=8; } while (op<(oend - LZ4Util.COPYLENGTH));;
                        while (op < cpy) *op++ = *r++;
                        op = cpy;
                        if (op == oend) break; // Check EOF (should never happen, since last 5 bytes are supposed to be literals)
                        continue;
                    }
                    if (op<cpy) do { *(ulong*)op = *(ulong*)r; op+=8; r+=8; } while (op<cpy);;
                    op = cpy; // correction
                }


                return (int)(((byte*)op) - decompressedBuffer);


            _output_error:
                return (int)(-(((byte*)ip) - compressedBuffer));
            }
        }
    }

    public static class LZ4DecompressorFactory
    {
        public static ILZ4Decompressor CreateNew()
        {
            if (IntPtr.Size == 4)
                return new LZ4Decompressor32();
            return new LZ4Decompressor64();
        }
    }

    /// <summary>
    /// Constants and methods shared by LZ4Compressor and LZ4Decompressor
    /// </summary>
    internal class LZ4Util
    {
        //**************************************
        // Constants
        //**************************************
        public const int COPYLENGTH = 8;
        public const int ML_BITS = 4;
        public const uint ML_MASK = ((1U << ML_BITS) - 1);
        public const int RUN_BITS = (8 - ML_BITS);
        public const uint RUN_MASK = ((1U << RUN_BITS) - 1);

        public static unsafe void CopyMemory(byte* dst, byte* src, long length)
        {
            while (length >= 16)
            {
                *(ulong*)dst = *(ulong*)src; dst += 8; src += 8;
                *(ulong*)dst = *(ulong*)src; dst += 8; src += 8;
                length -= 16;
            }

            if (length >= 8)
            {
                *(ulong*)dst = *(ulong*)src; dst += 8; src += 8;
                length -= 8;
            }

            if (length >= 4)
            {
                *(uint*)dst = *(uint*)src; dst += 4; src += 4;
                length -= 4;
            }

            if (length >= 2)
            {
                *(ushort*)dst = *(ushort*)src; dst += 2; src += 2;
                length -= 2;
            }

            if (length != 0)
                *dst = *src;
        }
    }
}
