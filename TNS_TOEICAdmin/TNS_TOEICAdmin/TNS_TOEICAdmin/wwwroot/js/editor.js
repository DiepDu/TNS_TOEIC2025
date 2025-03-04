// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function DateTimeJsonView(datetimefull, format) {
    if (datetimefull == null)
        return "";
    else {
   
        var Year = 0, Month = 0, Day = 0, HH = 0, mm = 0, ss = 0;
        var result = "None";
        var zVal = datetimefull.split("T");
        var zValDate = zVal[0].trim();
        var zValTime = zVal[1].trim();
        var zDate, zTime;  
        if (zValDate.length >= 8) {
            zDate = zValDate.split("-");
            if (zDate.length == 3) {
                Year = zDate[0];
                Month = zDate[1];
                Day = zDate[2];
            }
            else {
                zDate = zValDate.split("/");
                if (zDate.length == 3) {
                    Year = zDate[0];
                    Month = zDate[1];
                    Day = zDate[2];
                }
            }
        }
        else {
            return "Error";
        }
        if (Year < 1900)
            return "";
        if (zValTime.length > 0) {
            zTime = zValTime.split(":");
            if (zTime.length >= 1)
                HH = zTime[0];
            if (zTime.length >= 2)
                mm = zTime[1];
            if (zTime.length >= 3)
                ss = zTime[2];
        }
        var zFormat = format.split(" ");
        var zFormatDate = zFormat[0].trim();
        var zFormatTime = zFormat[1].trim();

        switch (zFormatDate) {
            case "dd/MM/yyyy":
                result = Day + "/" + Month + "/" + Year;
                break;
            case "dd/MM/yy":
                if (Year.length == 4)
                    result = Day + "/" + Month + "/" + Year.substring(2, 4);
                else
                    result = Day + "/" + Month + "/" + Year;
                break;
            case "dd-MM-yyyy":
                result = Day + "-" + Month + "-" + Year;
                break;
            case "dd-MM-yy":
                if (Year.length == 4)
                    result = Day + "-" + Month + "-" + Year.substring(2, 4);
                else
                    result = Day + "-" + Month + "-" + Year;
                break;
        }

        switch (zFormatTime) {
            case "HH:mm":
                result += " " + HH + ":" + mm;
                break;
            case "HH:mm:ss":
                result += " " + HH + ":" + mm + ":" + ss;
                break;
        }
    }
    return result;

}


function InitEditor(id) {
    tinymce.init(
        {
            selector: "textarea#" + id,
            height: 300,
            plugins: ["advlist autolink link image lists charmap print preview hr anchor pagebreak spellchecker", "searchreplace wordcount visualblocks visualchars code fullscreen insertdatetime media nonbreaking", "save table contextmenu directionality emoticons template paste textcolor"],
            toolbar: "insertfile undo redo | styleselect | bold italic | alignleft aligncenter alignright alignjustify | bullist numlist outdent indent | l      ink image | print preview media fullpage | forecolor backcolor emoticons",
            style_formats: [
                { title: "Bold text", inline: "b" },
                { title: "Red text", inline: "span", styles: { color: "#ff0000" } },
                { title: "Red header", block: "h1", styles: { color: "#ff0000" } },
                { title: "Example 1", inline: "span", classes: "example1" },
                { title: "Example 2", inline: "span", classes: "example2" },
                { title: "Table styles" }, { title: "Table row 1", selector: "tr", classes: "tablerow1" }]
        });
}