package p5.dreamteam.qr_reader;

import android.Manifest;
import android.annotation.SuppressLint;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.support.annotation.NonNull;
import android.support.v4.app.ActivityCompat;
import android.support.v7.app.AppCompatActivity;
import android.text.InputType;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.Toast;

import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

/**
 * Main class that the user meets on app launch.
 */
public class MainActivity extends AppCompatActivity {
    private static final int ZBAR_SCANNER_REQUEST = 0;
    private EditText _editTextToSend;
    private EditText _editIP;
    private EditText _editPort;
    private CheckBox _chkFlash;
    private CheckBox _chkSendImmediately;

    private final static String TAG = "MainActivity"; // Tag for logging
    private String _serverResponse;

    /**
     * Set layout of MainActivity and inflate important views
     * @param savedInstanceState Get previous activity state of activity if applicaple (e.g. when rotated)
     */
    @SuppressLint("SetTextI18n")
    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        _editTextToSend = findViewById(R.id.edit_textToSend);
        _editIP = findViewById(R.id.edit_ip);
        _editPort = findViewById(R.id.edit_port);
        _chkFlash = findViewById(R.id.chk_flash);
        _chkSendImmediately = findViewById(R.id.chk_sendImmediately);

        _editIP.setInputType(InputType.TYPE_CLASS_PHONE);
        _editPort.setInputType(InputType.TYPE_CLASS_PHONE);
        _editIP.setText("192.168.43.7");
        _editPort.setText("100");
    }

    /**
     * Check if camera exists using {@link #isCameraAvailable()}, check permission,
     * and launch activity if granted
     * @param v Button instance that was clicked
     */
    public void checkCamPermissionAndLaunchScanner(View v) {
        if (isCameraAvailable()) {
            // Request permissions and wait for user response
            ActivityCompat.requestPermissions(this, new String[]{Manifest.permission.CAMERA}, 0);
        } else {
            Toast.makeText(this, "Rear facing camera unavailable", Toast.LENGTH_SHORT).show();
        }
    }

    /**
     * Check if flash checkbox is checked, and launch {@link ZBarScannerActivity},
     * and send result to {@link #onActivityResult(int, int, Intent)}
     */
    private void launchScanner() {
        Intent intent = new Intent(this, ZBarScannerActivity.class);
        intent.putExtra("FLASH", _chkFlash.isChecked());
        startActivityForResult(intent, ZBAR_SCANNER_REQUEST);
    }

    /**
     * Wrapper to check if phone has a camera at all.
     * @return Whether or not camera exists
     */
    public boolean isCameraAvailable() {
        return getPackageManager().hasSystemFeature(PackageManager.FEATURE_CAMERA);
    }

    /**
     * Called when code is scanned and sends data to server if result is OK. Otherwise displays toast.
     * Also checks if data must be send immediately upon scan, or if it can be appended to {@link #_editTextToSend}.
     * @param requestCode A request for ZBar scanner. Trouble if not 0.
     * @param resultCode Is the result OK?
     * @param data The data that the scanner obtains. Converted to string and sent to server.
     */
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == ZBAR_SCANNER_REQUEST) {
            if (resultCode == RESULT_OK) {
                String dataToSend = data.getStringExtra(ZBarConstants.SCAN_RESULT);
                if (_chkSendImmediately.isChecked()) {
                    sendDataToServer(dataToSend);
                } else {
                    _editTextToSend.setText(dataToSend);
                }
            } else if(resultCode == RESULT_CANCELED && data != null) { // Result is not OK, show toast.
                String error = data.getStringExtra(ZBarConstants.ERROR_INFO);
                if(!TextUtils.isEmpty(error)) {
                    Toast.makeText(this, error, Toast.LENGTH_SHORT).show();
                }
            }
        } else {
            makeCentreToast("I knew you were trouble when you walked in\nOh, oh, trouble, trouble, trouble");
        }
    }

    /**
     * If needed, ask user for permission (in this case camera), and launch scanner if granted. Show toast if not.
     * @param grantResults In this case only one: Camera permission
     */
    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String permissions[],
                                           @NonNull int[] grantResults) {
        if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
            launchScanner();
        } else {
            Toast.makeText(this, "Camera permission denied. Cannot scan.", Toast.LENGTH_LONG).show();
        }
    }

    /**
     * Creates a new instance of {@link ConnectionTask} from IP and port specified.
     * Runs asynchronously due to Android forcing network on separate thread, but need to wait for response.
     * Timeout is 2 seconds.
     * @param data Data to send. Often result of ZBar scanning.
     * @return Error code. 0 if successful.
     */
    public int sendDataToServer(String data) {
        try {
            ConnectionTask task = new ConnectionTask(_editIP.getText().toString(),
                    Integer.parseInt(_editPort.getText().toString()), data);
            _serverResponse = task.execute().get(2, TimeUnit.SECONDS);
            // Horrible workaround to handle missing exceptions in other thread
            if (_serverResponse == null) {
                return 1;
            } else if (_serverResponse.equals("")) {
                return 2;
            } else if(_serverResponse.endsWith("<EOF>")) {
                return 0;
            } else
            {
                return 3;
            }
        } catch (InterruptedException e) {
            return 4;
        } catch (ExecutionException e) {
            return 5;
        } catch (TimeoutException e) {
            return 6;
        }
    }

    /**
     * Display a toast message in the centre of the screen with long duration.
     * @param message The message to be displayed.
     */
    private void makeCentreToast(String message) {
        Toast toast = Toast.makeText(this, message, Toast.LENGTH_LONG);
        toast.setGravity(Gravity.CENTER, 0, 0);
        toast.show();
    }

    /**
     * Called when user presses the "Send" button.
     * Sends data to server and handles error code with {@link #showToastFromSendDataErrorCode(int)}
     * @param view Button instance that was pressed.
     */
    public void sendDataButtonClick(View view) {
        showToastFromSendDataErrorCode(sendDataToServer(_editTextToSend.getText().toString()));
    }

    /**
     * Called when user presses the "Request route" button.
     * Sends data to server and handles error code with {@link #showToastFromSendDataErrorCode(int)}.
     * If no errors, start a new {@link VisualRepresentationActivity} with the server response as extra data.
     * @param view The button that was pressed.
     */
    public void requestButtonClick(View view){
        int errorCode = sendDataToServer("@req");
        if (errorCode != 0) {
            showToastFromSendDataErrorCode(errorCode);
            return;
        }
        Intent intent = new Intent(this, VisualRepresentationActivity.class);
        intent.putExtra("RepresentationData", _serverResponse);
        startActivity(intent);
    }

    /**
     * Shows toast from error code from {@link #sendDataToServer(String)}.
     * If no errors, clear {@link #_editTextToSend}.
     * @param errorCode The error code received.
     */
    private void showToastFromSendDataErrorCode(int errorCode) {
        switch (errorCode) {
            case 0:
                _editTextToSend.setText("");
                break;
            case 1:
                makeCentreToast("Server not found");
                break;
            case 2:
                makeCentreToast("Found IP, but server not responding");
                break;
            case 3:
                makeCentreToast("Message lost in during connection");
                break;
            case 4:
                makeCentreToast("Interrupted error");
                break;
            case 5:
                makeCentreToast("Execution error");
                break;
            case 6:
                makeCentreToast("Server timeout");
                break;
            default:
                makeCentreToast("Unknown error");
                break;
        }
    }
}
