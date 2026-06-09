import { AppBar, Box, Button, Container, Toolbar, Typography } from "@mui/material";
import { Outlet } from "react-router-dom";
import { useAuth } from "react-oidc-context";

export default function App() {
  const auth = useAuth();
  const name = (auth.user?.profile?.name as string | undefined) ?? auth.user?.profile?.sub;

  return (
    <Box sx={{ display: "flex", flexDirection: "column", minHeight: "100vh" }}>
      <AppBar position="static" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ fontWeight: 700, flexGrow: 1 }}>
            Paygate
          </Typography>
          {auth.isAuthenticated && (
            <>
              <Typography variant="body2" sx={{ mr: 2 }}>{name}</Typography>
              <Button color="inherit" onClick={() => void auth.signoutRedirect()}>
                Logout
              </Button>
            </>
          )}
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ mt: 4, mb: 4, flex: 1 }}>
        <Outlet />
      </Container>
    </Box>
  );
}
