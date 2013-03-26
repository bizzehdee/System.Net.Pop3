/*
 * Copyright (c) 2011, Darren Horrocks
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 * Redistributions of source code must retain the above copyright notice, this list 
 * of conditions and the following disclaimer.
 * Redistributions in binary form must reproduce the above copyright notice, this 
 * list of conditions and the following disclaimer in the documentation and/or 
 * other materials provided with the distribution.
 * Neither the name of Darren Horrocks/www.bizzeh.com nor the names of its 
 * contributors may be used to endorse or promote products derived from this software 
 * without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY 
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT 
 * SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, 
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, 
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF 
 * THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Security.Authentication;

namespace System.Net.Pop3
{
  public class Pop3Client
  {
    internal TcpClient m_client;
    internal bool m_use_ssl = false;
    internal SslStream m_ssl_stream = null;
    internal NetworkStream m_network_stream = null;
    internal Stream m_stream = null;

    public Pop3Client()
    {
      m_client = new TcpClient();
    }

    public void Connect(String server, int port, bool ssl)
    {
      String m_response = "";
      m_use_ssl = ssl;
      m_client.Connect(server, port);

      m_network_stream = m_client.GetStream();

      if (m_use_ssl)
      {
        m_ssl_stream = new SslStream((Stream)m_network_stream, true);
        m_ssl_stream.AuthenticateAsClient(server);

        m_stream = (Stream)m_ssl_stream;
      }
      else
      {
        m_stream = m_network_stream;
      }

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }
    }

    public void Connect(String server, int port)
    {
      this.Connect(server, port, false);
    }

    public void Connect(String server)
    {
      this.Connect(server, 110);
    }

    public void Disconnect()
    {
      String m_response = "";

      this.Write("QUIT");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }

      m_client.Close();
    }

    public void SendAuthUser(String user)
    {
      String m_response = "";

      this.Write("USER " + user);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }
    }

    public void SendAuthPass(String pass)
    {
      String m_response = "";

      this.Write("PASS " + pass);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }
    }

    public void SendAuthUserPass(String user, String pass)
    {
      SendAuthUser(user);
      SendAuthPass(pass);
    }

    public UInt32 GetEmailCount()
    {
      String m_response = "";
      UInt32 m_email_count = 0;

      this.Write("STAT");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }

      char[] m_seps = { ' ' };
      String[] m_parts = m_response.Split(m_seps);
      m_email_count = Convert.ToUInt32(m_parts[1]);

      return m_email_count;
    }

    //not sure this is needed
    public void GetEmailSizeList()
    {
      String m_response = "";

      this.Write("LIST");

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }

      while (true)
      {
        m_response = this.Response();

        if (m_response == ".\r\n" || m_response == ".\n")
        {
          break;
        }
        else
        {
          char[] m_seperator = { ' ' };
          String[] m_values = m_response.Split(m_seperator);

          
          continue;
        }
      }
    }

    public String GetEmailRaw(UInt32 id)
    {
      String m_response = "";
      StringBuilder m_email = new StringBuilder();

      this.Write("RETR " + id);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }

      while (true)
      {
        m_response = this.Response();

        if (m_response == ".\r\n" || m_response == ".\n")
        {
          break;
        }
        else
        {
          char[] m_seperator = { ' ' };
          String[] m_values = m_response.Split(m_seperator);

          m_email.AppendLine(m_response.TrimEnd());

          continue;
        }
      }

      return m_email.ToString();
    }

    public Pop3Message GetEmail(UInt32 id)
    {
      String m_message_raw = this.GetEmailRaw(id);

      Pop3Message m_message = new Pop3Message();

      m_message.ParseRawMessage(m_message_raw);

      return m_message;
    }

    public void DeleteEmail(UInt32 id)
    {
      String m_response = "";

      this.Write("DELE" + id);

      m_response = this.Response();

      if (m_response.Substring(0, 3) != "+OK")
      {
        throw new Pop3Exception(m_response);
      }
    }

    #region Internals

    internal void Write(String str)
    {
      ASCIIEncoding m_enc = new ASCIIEncoding();
      byte[] m_buf = new byte[1024];

      if (!str.EndsWith("\r\n")) str += "\r\n";

      m_buf = m_enc.GetBytes(str);

      m_stream.Write(m_buf, 0, m_buf.Length);
    }

    internal string Response()
    {
      String m_ret = "";
      ASCIIEncoding m_enc = new ASCIIEncoding();
      byte[] m_buf = new byte[1024];
      int m_count = 0;

      while (true)
      {
        byte[] m_rbuf = new byte[1];
        int m_bytes = m_stream.Read(m_rbuf, 0, 1);
        if (m_bytes == 1)
        {
          m_buf[m_count] = m_rbuf[0];
          m_count++;

          if (m_rbuf[0] == '\n')
          {
            break;
          }
        }
        else
        {
          break;
        }
      }

      m_ret = m_enc.GetString(m_buf, 0, m_count);

      return m_ret;
    }

    #endregion
  }
}
