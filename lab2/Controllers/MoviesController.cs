using Microsoft.AspNetCore.Mvc;

namespace web_services_l1.Controllers;

[ApiController]
[Route("[controller]")]
public class MoviesController : ControllerBase
{
    [HttpPost("UploadMovieCsv")]
    public string Post(IFormFile inputFile)
    {
        var strm = inputFile.OpenReadStream();
        byte[] buffer = new byte[inputFile.Length];
        strm.Read(buffer, 0, (int)inputFile.Length);
        string fileContent = System.Text.Encoding.Default.GetString(buffer);
        strm.Close();
        MoviesContext dbContext = new MoviesContext();
        bool skip_header = true;
        foreach (string line in fileContent.Split('\n'))
        {
            if (skip_header)
            {
                skip_header = false;
                continue;
            }
            var tokens = line.Split(",");
            if (tokens.Length != 3)
                continue;
            string MovieID = tokens[0];
            string MovieName = tokens[1];
            string[] Genres = tokens[2].Split("|");
            List<Genre> movieGenres = new List<Genre>();
            foreach (string genre in Genres)
            {
                Genre g = new Genre();
                g.Name = genre;
                if (!dbContext.Genres.Any(e => e.Name == g.Name))
                {
                    dbContext.Genres.Add(g);
                    dbContext.SaveChanges();
                }
                IQueryable<Genre> results = dbContext.Genres.Where(e => e.Name == g.Name);
                if (results.Count() > 0)
                    movieGenres.Add(results.First());
            }
            Movie m = new Movie();
            m.MovieID = int.Parse(MovieID);
            m.Title = MovieName;
            m.Genres = movieGenres;
            if (!dbContext.Movies.Any(e => e.MovieID == m.MovieID))
                dbContext.Movies.Add(m);
            dbContext.SaveChanges();
        }
        dbContext.SaveChanges();

        return "OK";
    }

    [HttpGet("GetAllGenres")]
    public IEnumerable<Genre> GetAllGenres()
    {
        MoviesContext dbContext = new MoviesContext();
        return dbContext.Genres.AsEnumerable();
    }

    [HttpGet("GetMoviesByName/{search_phrase}")]
    public IEnumerable<Movie> GetMoviesByName(string search_phrase)
    {
        MoviesContext dbContext = new MoviesContext();
        return dbContext.Movies.Where(e => e.Title.Contains(search_phrase));
    }

    [HttpGet("GetRelatedMoviesByID/{movie_id}")]
    public IEnumerable<Genre> GetGenresByMovieID(int movie_id)
    {
        MoviesContext dbContext = new MoviesContext();
        return dbContext.Genres.Where(m => m.Movies.Any(i => i.MovieID.Equals(movie_id)));
    }
    [HttpGet("GetGenreVectorByMovieID/{movie_id}")]
    public int[] GetGenreVectorByMovieID(int movie_id)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            int[] vec = new int[37];
            IEnumerable<Genre> Genres = dbContext.Genres.Where(m => m.Movies.Any(i => i.MovieID.Equals(movie_id))).ToList();
            IEnumerable<Genre> AllGenres = dbContext.Genres.AsEnumerable();

            int counter = 0;
            foreach (Genre ag in AllGenres)
            {
                foreach (Genre g in Genres)
                {
                    if (ag.Name == g.Name)
                    {
                        vec[counter] = 1;
                        break;
                    }
                    else
                    {
                        vec[counter] = 0;
                    }
                }
                counter++;
                if (counter == 37)
                {
                    break;
                }
            }
            return vec;
        }
    }
    [HttpGet("GetMoviesSimilarity/{id1}/{id2}")]
    public double GetMoviesSimilarity(int id1, int id2)
    {
        int[] genreVector1 = GetGenreVectorByMovieID(id1);
        int[] genreVector2 = GetGenreVectorByMovieID(id2);

        double product = 0;
        double sumA = 0;
        double sumB = 0;

        for (int i = 0; i < 37; i++)
        {
            product += genreVector1[i] * genreVector2[i];
            sumA += genreVector1[i] * genreVector1[i];
            sumB += genreVector2[i] * genreVector2[i];
        }
        double magnitudeA = Math.Sqrt(sumA);
        double magnitudeB = Math.Sqrt(sumB);

        double output = product / (magnitudeA * magnitudeB);

        return output;
    }

    //     MoviesContext dbContext = new MoviesContext();
    //     var movie = dbContext.Movies.FirstOrDefault(m => m.MovieID == movie_id);
    //     if (movie != null)
    //     {
    //         return movie.Genres.Select(g => g.Name).ToArray();
    //     }
    //     return null;
    // }

    [HttpGet("GetSimilarMovies/{movie_id}")]
    public IEnumerable<Movie> GetSimilarMovies(int movie_id)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            int[] vec1 = GetGenreVectorByMovieID(movie_id);
            IEnumerable<Movie> AllMovies = dbContext.Movies.AsEnumerable().ToList();

            List<Movie> movies = new List<Movie>();
            foreach (Movie am in AllMovies)
            {
                if (am.MovieID == movie_id)
                {
                    continue;
                }
                bool similar = false;
                int[] vec2 = GetGenreVectorByMovieID(am.MovieID);
                for (int i = 0; i < 37; i++)
                {
                    for (int j = 0; j < 37; j++)
                    {
                        if (vec1[i] == 1 && vec1[i] == vec2[j])
                        {
                            movies.Add(am);
                            similar = true;
                            break;
                        }
                    }

                    if (similar)
                    {
                        break;
                    }
                }
            }
            return movies;
        }
    }

    [HttpGet("GetSimilarMoviesThreshold/{movie_id}/{threshold}")]
    public IEnumerable<Movie> GetSimilarMoviesThreshold(int movie_id, double threshold)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            IEnumerable<Movie> AllMovies = dbContext.Movies.AsEnumerable().ToList();

            List<Movie> movies = new List<Movie>();
            foreach (Movie am in AllMovies)
            {
                if (am.MovieID == movie_id)
                {
                    continue;
                }
                double similarity = GetMoviesSimilarity(movie_id, am.MovieID);
                if (similarity > threshold)
                {
                    movies.Add(am);
                }
            }
            return movies;
        }
    }
    [HttpPost("GetMoviesByGenre")]
    public IEnumerable<Movie> GetMoviesByGenre(string search_phrase)
    {
        MoviesContext dbContext = new MoviesContext();
        return dbContext.Movies.Where(m => m.Genres.Any(p => p.Name.Contains(search_phrase)));
    }
    [HttpGet("GetMoviesRatedByUser/{user_id}")]
    public IEnumerable<Movie> GetMoviesRatedByUser(int user_id)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            List<Movie> movies = new List<Movie>();
            IEnumerable<Movie> AllMovies = dbContext.Movies.AsEnumerable().ToList();
            IEnumerable<Rating> ratings = dbContext.Ratings.Where(r => r.RatingUser.UserID.Equals(user_id));
            foreach (Movie am in AllMovies)
            {
                foreach (Rating r in ratings)
                {
                    if (am.MovieID==r.RatedMovie.MovieID)
                    {
                        movies.Add(am);
                    }
                }
            }

            return movies;
        }
    }

    [HttpGet("GetMoviesRatedByUserSorted/{user_id}")]
    public IEnumerable<Movie> GetMoviesRatedByUserSorted(int user_id)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            List<Movie> movies = new List<Movie>();
            IEnumerable<Movie> AllMovies = dbContext.Movies.AsEnumerable().ToList();
            IEnumerable<Rating> ratings = dbContext.Ratings.Where(r => r.RatingUser.UserID.Equals(user_id)).OrderByDescending(r => r.RatingValue).ToList();
            foreach (Movie am in AllMovies)
            {
                foreach (Rating r in ratings)
                {
                    if (am.MovieID==r.RatedMovie.MovieID)
                    {
                        movies.Add(am);
                    }
                }
            }

            return movies;
        }
    }
    [HttpGet("GetSimilarMoviesRatedByUser/{user_id}")]
    public IEnumerable<Movie> GetSimilarMoviesRatedByUser(int user_id)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            IEnumerable<Movie> allMovies = dbContext.Movies.AsEnumerable().ToList();
            Rating rating = dbContext.Ratings.Where(r => r.RatingUser.UserID.Equals(user_id)).OrderByDescending(r => r.RatingValue).ToList().First();

            return GetSimilarMovies(rating.RatedMovie.MovieID);
        }
    }

    [HttpGet("GetSimilarMoviesRatedByUser/{user_id}/{length}")]
    public IEnumerable<Movie> GetSimilarMoviesRatedByUser(int user_id, int length)
    {
        using (MoviesContext dbContext = new MoviesContext())
        {
            IEnumerable<Movie> allMovies = dbContext.Movies.AsEnumerable().ToList();
            IEnumerable<Rating> rating = dbContext.Ratings.Where(r => r.RatingUser.UserID.Equals(user_id)).OrderByDescending(r => r.RatingValue).ToList();
            List<Movie> returnmovies = new List<Movie>();
            int count = 0;
            bool stop = false;
            foreach(Rating r in rating){
                IEnumerable<Movie> movies = GetSimilarMovies(r.RatedMovie.MovieID);
                foreach(Movie m in movies){
                    if(r.RatedMovie.MovieID!=m.MovieID){
                        returnmovies.Add(m);
                        count = count+1;
                    }
                    if(count==length){
                        stop=true;
                        break;
                    }
                }
                if(stop){
                    break;
                }
            }
           return returnmovies;


        }
    }
}
