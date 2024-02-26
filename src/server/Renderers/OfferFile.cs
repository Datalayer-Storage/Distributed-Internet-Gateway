using QRCoder;

public class OfferRenderer
{
    public string RenderOffer(string offerFile)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(offerFile, QRCodeGenerator.ECCLevel.L);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        var base64Image = Convert.ToBase64String(qrCodeImage);
        string qrCodeImg = $"<a href=\"data:text/plain;charset=utf-8,{Uri.EscapeDataString(offerFile)}\" download=\"offerFile.txt\"><img src=\"data:image/png;base64,{base64Image}\" class=\"qr-image\" alt=\"QR Code\" title=\"Tap to download offer\" /></a>";

        string content = $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>QR Code for .offer File</title>
                <style>
              body, html {{
                margin: 0;
                padding: 0;
                height: 100%;
                display: flex;
                justify-content: center;
                align-items: center;
                flex-direction: column;
                font-family: Arial, sans-serif;
                background: #f0f0f0; /* Light background for contrast */
              }}
              .qr-container {{
                text-align: center;
                box-sizing: border-box;
                padding: 10px;
                border-radius: 15px; 
                background: white; 
                box-shadow: 0 2px 10px rgba(0,0,0,0.1); 
                margin: 20px; 
              }}
              .qr-image {{
                width: 500px;
                max-width: 90%; 
                height: auto;
                cursor: pointer; 
                border: solid thin grey; 
                border-radius: 10px; 
                padding: 10px;
                background: white; 
              }}
              .spinner {{
                display: none; 
              }}
              .toast {{
                visibility: hidden;
                min-width: 250px;
                background-color: #444; /* Darker background for better contrast */
                color: #fff;
                text-align: center;
                border-radius: 5px; /* Rounded corners */
                padding: 16px;
                position: fixed;
                z-index: 1;
                left: 50%;
                bottom: 30px;
                font-size: 17px;
                box-shadow: 0 4px 6px rgba(0,0,0,0.2); /* Subtle shadow for depth */
                transform: translateX(-50%);
                transition: visibility 0.3s, opacity 0.3s, bottom 0.3s; /* Smooth transitions for showing and hiding */
                opacity: 0;
              }}
              .show {{
                visibility: visible;
                opacity: 1;
                bottom: 50px;
              }}
              .embed-container {{
                width: 100%;
                margin-top: 20px;
                margin-left: 37px;
              }}
              img::before {{
                  content: 'Loading...';
                  width: 100%;
                  height: 500px;
                  display: flex;
                  justify-content: center;
                  align-items: center;
                  color: #333; 
                  background: rgba(255, 255, 255, 0.8); 
                  z-index: 2;
              }}
              img {{
                width: 100%;
                height: 100%;
                object-fit: cover; 
                top: 0;
                left: 0;
                z-index: 1; 
                opacity: 0; 
                transition: opacity 0.3s ease-in-out; 
              }}
              img.loaded {{
                  opacity: 1; /* Makes the image fully visible once loaded */
              }}
              textarea.embed-code {{
                width: 90%;
                height: 100px;
                padding: 10px; 
                border-radius: 10px;
                border: 1px solid #ccc; 
                background: #f9f9f9; /* Light background */
                font-family: monospace; 
                font-size: 14px; 
                resize: none; 
                box-shadow: inset 0 1px 3px rgba(0,0,0,0.1); 
              }}
              @keyframes spin {{
                to {{ transform: translate(-50%, -50%) rotate(360deg); }}
              }}
              @media screen and (max-width: 600px) {{
                html, body {{
                  height: 100%;
                  width: 100%;
                }}
                .qr-container {{
                  width: 95%;
                }}
                .embed-container {{
                  margin-left: 37px;
                }}
                .qr-image {{
                  max-width:  unset;
                  width: 90%; /* Ensure QR code takes full width on mobile screens */
                  margin: 10px; /* Ensure 10px border effect using margin for mobile screens */
                  min-width: auto; /* Override the min-width for mobile screens */
                }}
              }}
            </style>
            </head>
            <body>
                <div class='qr-container'>
                    <p>Tap the QR code to download the content:</p>
                    {qrCodeImg}
                </div>
                <div class='embed-container'>
                    <p>Embed Code:</p>
                    <textarea class='embed-code' id='embedCode' readonly></textarea>
                </div>
                <div id='toast' class='toast'>Content copied to clipboard!</div>
                <script>
                    function showToast(message) {{
                        const toast = document.getElementById('toast');
                        toast.textContent = message;
                        toast.className = 'show';
                        setTimeout(() => {{ toast.className = toast.className.replace('show', ''); }}, 3000);
                    }}

                    function hideSpinner(image) {{
                        const container = image.closest('.image-container');
                        if (container) {{
                            const spinner = container.querySelector('.spinner');
                            if (spinner) {{
                                spinner.style.display = 'none';
                            }}
                        }}
                    }}

                    document.addEventListener('DOMContentLoaded', function() {{
                        const embedCodeElement = document.getElementById('embedCode');
                        const embedCode = '{qrCodeImg}';
                        embedCodeElement.textContent = embedCode;

                        embedCodeElement.addEventListener('click', function() {{
                            this.select();
                            document.execCommand('copy');
                            showToast('Embed code copied to clipboard!');
                        }});
                    }});

                    document.querySelectorAll('img').forEach(img => {{
                        if (img.complete) img.classList.add('loaded');
                        img.onload = () => {{
                            img.classList.add('loaded');
                        }};
                    }});
                </script>
            </body>
            </html>";

        return content;

    }
}
