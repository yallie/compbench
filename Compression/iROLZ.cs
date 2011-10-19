// (C) 2010, Andrew Polar under GPL ver. 3.
//   LICENSE
//
//   This program is free software; you can redistribute it and/or
//   modify it under the terms of the GNU General Public License as
//   published by the Free Software Foundation; either version 3 of
//   the License, or (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful, but
//   WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//   General Public License for more details at
//   Visit <http://www.gnu.org/copyleft/gpl.html>.
//
// This is complete DEMO for iROLZ algorithm. 
//
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

public static class iROLZ
{
    const int PREFIX_SIZE = 3;
    const int HISTORY_BUFFER_BITLEN = 18;  
    const int SUFFICIENT_MATCH = 64;
    const int MINIMUM_MATCH = 4;
    const int LONGEST_COUNT = 255;
    public const int BIT_LEN_FOR_STEPS = 5;

    const byte TERMINATION_FLAG = (1 << BIT_LEN_FOR_STEPS) - 1;
    const int CHUNK_SIZE = 0xffffff + 1;
    static int MAX_STEPS = TERMINATION_FLAG - 1;

    //This function is taken from open source http://balz.sourceforge.net/,
    //written by Ilia Muraviev. It is preprocessing for exe and DLLs.
    private static void exetransform(byte[] buf, int mode, int size) 
    {
	    int end = size - 8;
	    int i = 0;
        short context = (short)((buf[i + 1] << 8) | buf[i]);
        while (context != 0x4550 && i < end) 
        {
            ++i;
            context = (short)((buf[i + 1] << 8) | buf[i]);
        }
	    while (i < end) 
        {
		    if ((buf[i++] & 254) == 0xe8) 
            {
                int word = (buf[i + 3] << 24) | (buf[i + 2] << 16) | (buf[i + 1] << 8) | buf[i];
                if (mode > 0)
                {
                    if ((word >= -i) && (word < (size - i)))
                    {
                        word += i;
                    }
                    else
                    {
                        if ((word > 0) && (word < size)) word -= size;
                    }
                }
                else
                {
                    if (word < 0)
                    {
                        if ((word + i) >= 0) word += size;
                    }
                    else
                    {
                        if (word < size) word -= i;
                    }
                }
                buf[i] = (byte)(word & 0xff);
                buf[i + 1] = (byte)((word >> 8) & 0xff);
                buf[i + 2] = (byte)((word >> 16) & 0xff);
                buf[i + 3] = (byte)((word >> 24) & 0xff);
                i += 4;
		    }
	    }
    }
    //Function finds longest match
    private static byte getLongestMatch(Dictionary dictionary, byte[] data, int index, int data_size, out byte nsteps)
    {
        nsteps = 0;
        if (index + MINIMUM_MATCH >= data_size) return 0;
        byte max_count, max_step, step, count;
        int position = index;
        max_count = max_step = step = 0;
        while (true)
        {
            int new_position = dictionary.getNextPosition(position);
            if (new_position >= position) break;
            if (data[index + MINIMUM_MATCH] == data[new_position + MINIMUM_MATCH]) //quick test
            { 
                count = 0;
                while (true)
                {
                    if (index + 1 + count >= data_size) break;
                    if (data[index + 1 + count] == data[new_position + 1 + count])
                    {
                        ++count;
                    }
                    else break;
                    if (count >= LONGEST_COUNT) break;
                }
                if (count > max_count)
                {
                    max_count = count;
                    max_step = step;
                }
                if (max_count > SUFFICIENT_MATCH) break;
            }
            if (step++ >= MAX_STEPS) break;
            position = new_position;
        }
        nsteps = max_step;
        return max_count;
    }
    private static bool process_compress(TPPM ppm, Dictionary dictionary, byte[] data, int data_size, bool isLast)
    {
        int index = 0;
        byte max_count, max_step, prev_flag, prev_count, context;
        prev_flag = prev_count = context = 0;
        do
        {
            dictionary.updateDictionary(data[index]);
            ppm.EncodeBit(0, prev_flag);
            prev_flag = 0;
            ppm.EncodeLiteral(data[index], context);
            context = data[index];
            while (true)
            {
                max_count = getLongestMatch(dictionary, data, index, data_size, out max_step);
                if (max_count >= MINIMUM_MATCH)
                {
                    ppm.EncodeBit(1, prev_flag);
                    prev_flag = 1;
                    ppm.EncodeStep(max_step);
                    ppm.EncodeLength(max_count, prev_count);
                    prev_count = max_count;
                    //This fragment populates dictionay with values that sitting in 
                    //<offset, length> couple. 
                    for (int k = 0; k < max_count; ++k)
                    {
                        ++index;
                        dictionary.updateDictionary(data[index]);
                    }
                    context = data[index];
                }
                else
                {
                    break;
                }
            }
            ++index;
        } while (index < data_size);
        //We set termination flag that can be recognized without mistake. 
        if (isLast)
        {
            ppm.EncodeBit(1, prev_flag);
            ppm.EncodeStep(TERMINATION_FLAG);
            ppm.encoder.Flush();
        }
        return true;
    }
    private static int process_decompress(TPPM ppm, Dictionary dictionary, byte[] data, out int index)
    {
        index = 0;
        byte flag, prev_count, context;
        prev_count = context = flag = 0;
        while (true)
        {
            flag = ppm.DecodeBit(flag);
            if (flag == 0)
            {
                data[index] = ppm.DecodeLiteral(context);
                context = data[index];
                dictionary.updateDictionary(data[index]);
                ++index;
                if (index >= CHUNK_SIZE) return 1;
            }
            else
            {
                byte step = ppm.DecodeStep();
                if (step == TERMINATION_FLAG) return 0;
                //
                byte length = ppm.DecodeLength(prev_count);
                prev_count = length;

                int offset = 0;
                int position = index - 1;
                for (int k = 0; k <= step; ++k)
                {
                    position = dictionary.getNextPosition(position);
                }
                offset = index - 1 - position;
                for (int k = 0; k < length; ++k)
                {
                    data[index] = data[index - offset];
                    dictionary.updateDictionary(data[index]);
                    context = data[index];
                    ++index;
                    if (index >= CHUNK_SIZE) return 1;
                }
            }
        }
    }

    public static byte[] IrolzCompress(byte[] source)
    {
        var inStream = new MemoryStream(source);
        var outStream = new MemoryStream();
        
        var dictionary = new Dictionary(PREFIX_SIZE, HISTORY_BUFFER_BITLEN);
        var ppm = new TPPM(inStream, outStream);

        //we read and process data by large chunks
        var data = new byte[CHUNK_SIZE];
        while (true) 
        {          
            int data_size = inStream.Read(data, 0, CHUNK_SIZE);
            bool isLast = false;
            if (data_size < CHUNK_SIZE) isLast = true;
            exetransform(data, 1, data_size);
            dictionary.eraseData();
            bool res_flag = process_compress(ppm, dictionary, data, data_size, isLast);
            if (isLast) break;
        }

        var retval = outStream.ToArray();
        outStream.Close();
        return retval;
    }

    public static byte[] IrolzDecompress(byte[] source)
    {
        var inStream = new MemoryStream(source);
        var outStream = new MemoryStream();

        Dictionary dictionary = new Dictionary(PREFIX_SIZE, HISTORY_BUFFER_BITLEN);
        TPPM ppm = new TPPM(inStream, outStream);
        ppm.decoder.Init();
        //we decode data by large chunks
        byte[] data = new byte[CHUNK_SIZE];
        int data_size = CHUNK_SIZE;
        while (true) {
            dictionary.eraseData();
            int status = process_decompress(ppm, dictionary, data, out data_size);
            exetransform(data, 0, data_size);
            outStream.Write(data, 0, data_size);
            if (status == 0) break;
        }

        var retval = outStream.ToArray();
        outStream.Close();
        return retval;
    }

    public static bool irolz_compress(string inFile, string outFile)
    {
        if (!File.Exists(inFile))
        {
            Console.WriteLine("Input file {0} not found", inFile);
            return false;
        }
        if (File.Exists(outFile))
        {
            File.Delete(outFile);
        }
        FileStream inStream = null;
        FileStream outStream = null;
        try
        {
            inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
        Dictionary dictionary = new Dictionary(PREFIX_SIZE, HISTORY_BUFFER_BITLEN);
        TPPM ppm = new TPPM(inStream, outStream);
        //we read and process data by large chunks
        byte[] data = new byte[CHUNK_SIZE];
        while (true) 
        {          
            int data_size = inStream.Read(data, 0, CHUNK_SIZE);
            bool isLast = false;
            if (data_size < CHUNK_SIZE) isLast = true;
            exetransform(data, 1, data_size);
            dictionary.eraseData();
            bool res_flag = process_compress(ppm, dictionary, data, data_size, isLast);
            if (isLast) break;
        }
        inStream.Close();
        outStream.Close();
        return true;
    }
    public static bool irolz_decompress(string inFile, string outFile)
    {
        if (!File.Exists(inFile))
        {
            Console.WriteLine("Input file {0} not found", inFile);
            return false;
        }
        if (File.Exists(outFile))
        {
            File.Delete(outFile);
        }
        FileStream inStream = null;
        FileStream outStream = null;
        try
        {
            inStream = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            outStream = new FileStream(outFile, FileMode.Create, FileAccess.Write);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
        Dictionary dictionary = new Dictionary(PREFIX_SIZE, HISTORY_BUFFER_BITLEN);
        TPPM ppm = new TPPM(inStream, outStream);
        ppm.decoder.Init();
        //we decode data by large chunks
        byte[] data = new byte[CHUNK_SIZE];
        int data_size = CHUNK_SIZE;
        while (true) {
            dictionary.eraseData();
            int status = process_decompress(ppm, dictionary, data, out data_size);
            exetransform(data, 0, data_size);
            outStream.Write(data, 0, data_size);
            if (status == 0) break;
        }
        inStream.Close();
        outStream.Close();
        return true;
    }
}

//This is my portable dictionary class.
public class Dictionary 
{
	private int m_prefix_mask, m_buffer_mask, m_context, m_index;
    private int[] m_last_position_lookup = null;
	private int[] m_self_addressed_dictionary = null;
	public Dictionary(int PrefixSize, int OffsetLen) 
    {
	    if (PrefixSize == 1) m_prefix_mask = 0xff;
	    else if (PrefixSize == 2) m_prefix_mask = 0xffff;
	    else m_prefix_mask = 0xffffff;
	    m_buffer_mask = (1<<OffsetLen) - 1;

	    m_last_position_lookup = new int[m_prefix_mask + 1];
        m_self_addressed_dictionary = new int[m_buffer_mask + 1];
	    eraseData();
    }
    public void updateDictionary(byte b) 
    {
	    m_context <<= 8;
	    m_context |= b;
	    m_context &= m_prefix_mask;
	    m_self_addressed_dictionary[m_index & m_buffer_mask] = m_last_position_lookup[m_context];
	    m_last_position_lookup[m_context] = m_index;
	    ++m_index;
    }
    public void eraseData() 
    {
        for (int i = 0; i < m_prefix_mask + 1; ++i)
        {
            m_last_position_lookup[i] = 0x00;
        }
        for (int i = 0; i < m_buffer_mask + 1; ++i)
        {
            m_self_addressed_dictionary[i] = 0x00;
        }
	    m_context = m_index = 0;
    }
    public int getNextPosition(int position)
    {
        return m_self_addressed_dictionary[position & m_buffer_mask];
    }
}

//Predictor of Ilia Muraviev. Source: http://balz.sourceforge.net/
public class TPredictor {
    private ushort p1, p2;
	public TPredictor() 
    {
        p1 = 1 << 15;
        p2 = 1 << 15;
    }
	public int P()	
    {
		return (p1 + p2); 
	}
	public void Update(int bit) 
    { 
		if (bit > 0) {
			p1 += (ushort)((ushort)(~p1) >> 3); 
			p2 += (ushort)((ushort)(~p2) >> 6); 
		}
		else {
			p1 -= (ushort)(p1 >> 3); 
			p2 -= (ushort)(p2 >> 6); 
		}
	}
}

//This is Matt Mahoney's binary entropy coder from FPAQ0
public class BinaryEncoder 
{
    private Stream m_outStream = null;
    private uint x1, x2;
	public BinaryEncoder(Stream outStream) 
    {
        m_outStream = outStream;
        x1 = 0;
        x2 = 0xffffffff;
    }
	public void Encode(int P, int bit) 
    { 
		uint xmid = x1 + (uint)(((long)(x2 - x1) * (long)(P)) >> 17);
		if (bit > 0) x2 = xmid;
		else x1 = xmid + 1;

		while ((x1 ^ x2) < (1 << 24)) {
			m_outStream.WriteByte((byte)(x2 >> 24));
			x1 <<= 8;
            x2 = (x2 << 8) | 0xff;
		}
	}
	public void Flush() { 
		for (int i=0; i<4; i++) 
        {
			m_outStream.WriteByte((byte)(x2 >> 24));
			x2 <<= 8;
		}
	}
}

//This is Matt Mahoney's binary entropy decoder from FPAQ0
public class BinaryDecoder 
{
    private Stream m_inStream = null;
    private uint x1, x2, x;
    public BinaryDecoder(Stream inStream)
    {
        m_inStream = inStream;
        x1 = 0;
        x2 = 0xffffffff;
    }
	public void Init() 
    { 
		for (int i=0; i<4; i++) 
        {
			x = (x << 8) | ((byte)m_inStream.ReadByte());
		}
	}
	public byte Decode(int P) 
    {    
		uint xmid = x1 + (uint)(((long)(x2 - x1) * (long)(P)) >> 17);
		bool bit = (x <= xmid);
		if (bit) x2 = xmid;
		else x1 = xmid + 1;
		while ((x1 ^ x2) < (1 << 24)) 
        { 
			x1 <<= 8;
			x2 = (x2 << 8) | 255;
			x = (x << 8) | ((byte)m_inStream.ReadByte());
		}
        if (bit) return 1;
        else return 0;
	}
}

//PPM coder. Source: http://balz.sourceforge.net/
public class TPPM {
    private TPredictor[][] pliteral = null; 
    private TPredictor[][] plength = null; 
    private TPredictor[] pstep = null; 
    private TPredictor[] pbit = null; 
    public BinaryEncoder encoder = null; 
    public  BinaryDecoder decoder = null; 
    public TPPM(Stream inStream, Stream outStream)
    {
        pbit = new TPredictor[2]; 
        pbit[0] = new TPredictor();
        pbit[1] = new TPredictor();
        //
        encoder = new BinaryEncoder(outStream);
        decoder = new BinaryDecoder(inStream);
        //
        pstep = new TPredictor[256];
        pliteral = new TPredictor[256][];
        plength = new TPredictor[256][];
        for (int i = 0; i < 256; ++i)
        {
            pstep[i] = new TPredictor();
            pliteral[i] = new TPredictor[256];
            plength[i] = new TPredictor[256];
            for (int j = 0; j < 256; ++j)
            {
                pliteral[i][j] = new TPredictor();
                plength[i][j] = new TPredictor();
            }
        }
    }
    public void EncodeBit(byte bit, byte context) 
    {
        encoder.Encode(pbit[context].P(), bit);
        pbit[context].Update(bit);
    }
    public void EncodeLiteral(byte value, byte context)
    {
        for (int i = 7, j = 1; i >= 0; i--)
        {
            int bit = (value >> i) & 1;
            encoder.Encode(pliteral[context][j].P(), bit);
            pliteral[context][j].Update(bit);
            j += j + bit;
        }
    }
    public void EncodeLength(byte value, byte context)
    {
        for (int i = 7, j = 1; i >= 0; i--)
        {
            int bit = (value >> i) & 1;
            encoder.Encode(plength[context][j].P(), bit);
            plength[context][j].Update(bit);
            j += j + bit;
        }
    }
    public void EncodeStep(byte value)
    {
        int len = iROLZ.BIT_LEN_FOR_STEPS;
        for (int i = len - 1, j = 1; i >= 0; i--)
        {
            int bit = (value >> i) & 1;
            encoder.Encode(pstep[j].P(), bit);
            pstep[j].Update(bit);
            j += j + bit;
        }
    }
    public byte DecodeBit(byte context)
    {
        byte bit = decoder.Decode(pbit[context].P());
        pbit[context].Update(bit);
        return bit;
    }
    public byte DecodeLiteral(byte context)
    {
        int value = 1;
        do
        {
            byte bit = decoder.Decode(pliteral[context][value].P());
            pliteral[context][value].Update(bit);
            value += (value + bit);
        } while (value < 256);
        return (byte)(value - 256);
    }
    public byte DecodeLength(byte context)
    {
        int value = 1;
        do
        {
            byte bit = decoder.Decode(plength[context][value].P());
            plength[context][value].Update(bit);
            value += (value + bit);
        } while (value < 256);
        return (byte)(value - 256);
    }
    public byte DecodeStep()
    {
        int len = iROLZ.BIT_LEN_FOR_STEPS;
        int value = 1;
        do
        {
            byte bit = decoder.Decode(pstep[value].P());
            pstep[value].Update(bit);
            value += (value + bit);
        } while (value < (1 << len));
        return (byte)(value - (1 << len));
    }
}

class Entry
{
    static void DisabledMain(string[] args)
    {
        Console.WriteLine("Program name: iROLZ, author: Andrew Polar under GPL ver. 3. LICENSE");
        if (args.Length != 3)
        {
            Console.WriteLine("Usage for compression  :   irolz e input output");
            Console.WriteLine("Usage for decompression:   irolz d input output");
            Environment.Exit(0);
        }

        if (args[0] == "e")
        {
            DateTime start = DateTime.Now;
            //irolz_compress must be thread safe 
            bool res = iROLZ.irolz_compress(args[1], args[2]);
            DateTime end = DateTime.Now;
            TimeSpan duration = end - start;
            double time = duration.Minutes * 60.0 + duration.Seconds + duration.Milliseconds / 1000.0;
            Console.WriteLine("Time for encoding: {0:####.00} seconds", time);
            if (res)
            {
                FileInfo fiRes = new FileInfo(args[2]);
                long res_size = fiRes.Length;
                FileInfo fiSrc = new FileInfo(args[1]);
                long src_size = fiSrc.Length;
                if (src_size == 0) src_size = 1;
                Console.WriteLine("Compression ratio {0:0.000} relative to original", ((double)(res_size)) / ((double)(src_size)));
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("Failed to compress file");
                Environment.Exit(0);
            }
        }
        else if (args[0] == "d")
        {
            DateTime start = DateTime.Now;
            //irolz_decompress must be thread safe
            bool res = iROLZ.irolz_decompress(args[1], args[2]);
            DateTime end = DateTime.Now;
            TimeSpan duration = end - start;
            double time = duration.Minutes * 60.0 + duration.Seconds + duration.Milliseconds / 1000.0;
            Console.WriteLine("Time for decoding {0:####.00} seconds", time);
            if (res)
            {
                FileInfo fiRes = new FileInfo(args[2]);
                long res_size = fiRes.Length;
                FileInfo fiSrc = new FileInfo(args[1]);
                long src_size = fiSrc.Length;
                if (res_size == 0) res_size = 1;
                Console.WriteLine("Compression ratio {0:0.000} relative to original", ((double)(src_size)) / ((double)(res_size)));
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("Failed to decompress file");
                Environment.Exit(0);
            }
        }
        else
        {
            Console.WriteLine("Misformatted command string");
            Environment.Exit(0);
        }
    }
}