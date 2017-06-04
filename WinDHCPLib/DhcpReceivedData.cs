using System;

namespace WinDHCP.Library
{
    internal class DhcpReceivedData
    {
        private byte[] m_MessageBuffer;
        private int m_BufferSize;
        private IAsyncResult m_Result;

        public byte[] MessageBuffer
        {
            get { return this.m_MessageBuffer; }
            //set { this.m_MessageBuffer = value; }
        }

        public int BufferSize
        {
            get
            {
                return this.m_BufferSize;
            }

            set
            {
                this.m_BufferSize = value;

                byte[] oldBuffer = this.m_MessageBuffer;
                this.m_MessageBuffer = new byte[this.m_BufferSize];

                int copyLen = Math.Min(oldBuffer.Length, this.m_BufferSize);
                Array.Copy(oldBuffer, this.m_MessageBuffer, copyLen);
            }
        }

        public IAsyncResult Result
        {
            get { return this.m_Result; }
            set { this.m_Result = value; }
        }

        public DhcpReceivedData(byte[] messageBuffer)
        {
            this.m_MessageBuffer = messageBuffer;
            this.m_BufferSize = messageBuffer.Length;
        }
    }
}
