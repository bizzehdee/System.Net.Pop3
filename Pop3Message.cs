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

namespace System.Net.Pop3
{
  public class Pop3Message
  {
    public Pop3AttachmentList Attachments { get; set; }
    public Pop3HeaderList Headers { get; set; }
    public String Body { get; set; }
    public String BodyText { get; set; }
    public String BodyHtml { get; set; }
    public String Subject { get; set; }
    public String From { get; set; }
    public String To { get; set; }
    public String ReplyTo { get; set; }
    public double MimeVersion { get; set; }
    public String ContentType { get; set; }
    public String ContentBoundary { get; set; }
    public DateTime Date { get; set; }
    public bool IsReply { get; set; }
    public bool IsReceipt { get; set; }
    public String Raw { get; set; }

    public delegate bool MimeTypeHandlerCB(String mimetype, String[] lines, ref int start, Pop3Message msg);
    static public MimeTypeHandlerCB MimeTypeHandler;
    
    public Pop3Message()
    {
      Attachments = new Pop3AttachmentList();
      Headers = new Pop3HeaderList();
      Body = "";
      BodyText = "";
      BodyHtml = "";
      Subject = "";
      From = "";
      To = "";
      ReplyTo = "";
      MimeVersion = 0;
      ContentType = "text/plain";
      ContentBoundary = "";
      Date = DateTime.Now;
      IsReply = false;
      IsReceipt = false;

    }

    internal void ParseMessageSection(String bound, String[] lines, ref int i)
    {
      for (; i < lines.Length; i++)
      {
        if (lines[i] == "--" + bound + "--") return; //its all over for this section

        if (lines[i] == "--" + bound)
        {
          //beginning of the section, start looking for headers
          String m_current_type = "text/plain";
          String m_new_bound = "";
          String m_filename = "";
          bool is_attachment = false;

          while (lines[i] != "")
          {
            if (lines[i].StartsWith("Content-Type"))
            {
              String[] m_parts = lines[i].Split(':');
              String[] m_bits = m_parts[1].Split(';');
              m_current_type = m_bits[0].Trim();

              for (int x = 0; x < m_bits.Length; x++)
              {
                m_bits[x] = m_bits[x].Trim();
                if (m_bits[x].StartsWith("boundary=\""))
                {
                  m_new_bound = m_bits[x].Substring(10, m_bits[x].Length - 11);
                }
                else if (m_bits[x].StartsWith("boundary"))
                {
                  m_new_bound = m_bits[x].Substring(9, m_bits[x].Length - 9);
                }
              }
            }
            else if (lines[i].StartsWith("Content-Disposition"))
            {
              String[] m_parts = lines[i].Split(':');
              String[] m_bits = m_parts[1].Split(';');
              is_attachment = m_bits[0].Trim() == "attachment" ? true : false;

              for (int x = 0; x < m_bits.Length; x++)
              {
                m_bits[x] = m_bits[x].Trim();
                if (m_bits[x].StartsWith("filename=\""))
                {
                  m_filename = m_bits[x].Substring(10, m_bits[x].Length - 11);
                }
                else if (m_bits[x].StartsWith("filename="))
                {
                  m_filename = m_bits[x].Substring(9, m_bits[x].Length - 9);
                }
              }
            }

            i++;
          }

          if (m_new_bound != "")
          {
            //check for new section
            ParseMessageSection(m_new_bound, lines, ref i);
          }

          if (MimeTypeHandler == null || MimeTypeHandler(m_current_type, lines, ref i, this) == false)
          {
            StringBuilder m_bld = new StringBuilder();

            for (; i < lines.Length; i++)
            {
              if (lines[i] == "--" + bound) break;
              if (lines[i] == "--" + bound + "--") break;
              if (is_attachment)
              {
                m_bld.Append(lines[i]);
              }
              else
              {
                m_bld.AppendLine(lines[i]);
              }
            }

            i--;
            if (is_attachment)
            {
              Pop3Attachment m_attach = new Pop3Attachment();
              m_attach.Name = m_filename;
              m_attach.Type = m_current_type;
              m_attach.Data = Convert.FromBase64String(m_bld.ToString());

              //add to attachment list
              Attachments.Add(m_attach);
            } 
            else if (m_current_type == "text/plain")
            {
              this.BodyText = m_bld.ToString();
            }
            else if (m_current_type == "text/html")
            {
              this.BodyHtml = m_bld.ToString();
            }
          }

        }
      }
    }

    public void ParseRawMessage(String raw)
    {
      char[] m_seps = { '\n' };
      char[] m_hseps = { ':' };
      char[] m_cseps = { ';' };

      Raw = raw;

      String[] m_lines = raw.Split(m_seps);

      for (int x = 0; x < m_lines.Length; x++)
      {
        m_lines[x] = m_lines[x].TrimEnd('\r');
      }

      String m_current_header = "";
      int i=0;
      for (i = 0; i < m_lines.Length; i++)
      {
        if (m_lines[i].Trim().Length == 0) break;

        m_current_header = m_lines[i].Trim();

        while (m_lines[i + 1].StartsWith("\t") || m_lines[i + 1].StartsWith(" "))
        {
          m_current_header = m_current_header.Trim() + " " + m_lines[++i].Trim();
        }

        
        String[] m_part = m_current_header.Split(m_hseps, 2);

        System.Diagnostics.Debug.WriteLine(m_current_header);

        Headers.Add(new Pop3Header(m_part[0], m_part[1].Trim()));
      }

      //find some special headers to make them easier to use
      foreach (Pop3Header h in Headers)
      {
        if (h.Name.ToLower() == "subject")
        {
          Subject = h.Value;
        }
        else if (h.Name.ToLower() == "to")
        {
          To = h.Value;
        }
        else if (h.Name.ToLower() == "from")
        {
          From = h.Value;
        }
        else if (h.Name.ToLower() == "reply-to")
        {
          ReplyTo = h.Value;
        }
        else if (h.Name.ToLower() == "mime-version")
        {
          MimeVersion = Convert.ToDouble(h.Value);
        }
        else if (h.Name.ToLower() == "date")
        {
          try
          {
            Date = DateTime.Parse(h.Value);
          }
          catch (Exception)
          {
            Date = DateTime.Now;
          }
        }
        else if (h.Name.ToLower() == "content-type")
        {
          String m_type = h.Value;
          if (m_type.Contains(";"))
          {
            //ContentBoundary
            String[] m_cparts = m_type.Split(m_cseps);

            ContentType = m_cparts[0];

            for (int x = 1; x < m_cparts.Length; x++)
            {
              m_cparts[x] = m_cparts[x].Trim();
              if (m_cparts[x].StartsWith("boundary=\""))
              {
                ContentBoundary = m_cparts[x].Substring(10, m_cparts[x].Length - 11);
              }
              else if (m_cparts[x].StartsWith("boundary"))
              {
                ContentBoundary = m_cparts[x].Substring(9, m_cparts[x].Length - 9);
              }
            }
          }
          else
          {
            ContentType = h.Value;
          }
        }
      }

      if (Headers["References"] != null || Headers["In-Reply-To"] != null)
      {
        IsReply = true;
      }

      //do we have an outside parser?
      //if so, ask them do they want to handle this message type
      if (MimeTypeHandler == null || MimeTypeHandler(ContentType, m_lines, ref i, this) == false)
      {
        if (ContentType == "text/plain")
        {
          //plain text message
          StringBuilder m_body_text = new StringBuilder();
          for (i++; i < m_lines.Length; i++)
          {
            m_body_text.AppendLine(m_lines[i].TrimEnd());
          }
          BodyText = m_body_text.ToString();
        }
        else if (ContentType == "text/html")
        {
          //plain html message
          StringBuilder m_body_html = new StringBuilder();
          for (i++; i < m_lines.Length; i++)
          {
            m_body_html.AppendLine(m_lines[i].TrimEnd());
          }
          BodyHtml = m_body_html.ToString();
        }
        else
        {
          //not a plain and simple message, so we will handle it a different way
          ParseMessageSection(ContentBoundary, m_lines, ref i);
        }
      }
      
    }
  }
}
